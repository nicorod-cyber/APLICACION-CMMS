using System.Text.Json;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Alerts;

public sealed class AlertService : IAlertService
{
    private readonly CmmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;
    private readonly IEmailService _emailService;
    private readonly IPdfService _pdfService;
    private readonly PdfTemplateService _templateService;

    public AlertService(CmmsDbContext dbContext, IAuditService auditService, IAuthorizationPolicyService authorizationPolicyService, IEmailService emailService, IPdfService pdfService, IPdfTemplateService templateService)
    {
        _dbContext = dbContext; _auditService = auditService; _authorizationPolicyService = authorizationPolicyService; _emailService = emailService; _pdfService = pdfService;
        _templateService = templateService as PdfTemplateService ?? throw new InvalidOperationException("La implementacion de plantillas debe usar PostgreSQL.");
    }

    public async Task<IReadOnlyCollection<AlertResponse>> ListAsync(AlertQuery query, UserAccessContext user, CancellationToken cancellationToken)
    {
        var items = await _dbContext.Alerts.AsNoTracking().Include(item => item.AlertRule).Include(item => item.Faena)
            .Where(item => query.IncludeResolved || item.Status != AlertStatus.Resolved.ToString())
            .Where(item => !query.Status.HasValue || item.Status == query.Status.Value.ToString())
            .Where(item => !query.Severity.HasValue || item.Severity == query.Severity.Value.ToString())
            .Where(item => string.IsNullOrWhiteSpace(query.Source) || item.Source == query.Source.Trim())
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || item.Faena != null && item.Faena.Code == query.FaenaCodigo.Trim())
            .OrderByDescending(item => item.Severity).ThenByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc).ToArrayAsync(cancellationToken);
        return items.Select(ToAlertResponse).Where(item => CanViewAlert(item, user)).ToArray();
    }

    public async Task<AlertResponse> GenerateAsync(GenerateAlertRequest request, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanManage(user); ValidateGenerate(request); await EnsureDefaultsAsync(cancellationToken);
        var rule = await _dbContext.AlertRules.Include(item => item.Template).Include(item => item.Faena).Include(item => item.Recipients)
            .SingleOrDefaultAsync(item => item.Code == request.RuleCode && item.IsEnabled, cancellationToken)
            ?? throw new DomainException($"La regla de alerta '{request.RuleCode}' no existe o no esta habilitada.");
        var faena = await ResolveFaenaAsync(request.FaenaCodigo, cancellationToken);
        if (rule.FaenaId.HasValue && rule.FaenaId != faena?.Id) throw new DomainException("La regla no aplica a la faena solicitada.");
        var key = $"{rule.Code}:{request.CauseKey.Trim()}";
        var alert = await _dbContext.Alerts.Include(item => item.AlertRule).Include(item => item.Faena)
            .SingleOrDefaultAsync(item => item.AlertRuleId == rule.Id && item.DeduplicationKey == key && item.IsActive, cancellationToken);
        var isNew = alert is null;
        if (alert is null)
        {
            alert = new AlertEntity { AlertRuleId = rule.Id, AlertRule = rule, Title = request.Title.Trim(), Message = request.Message.Trim(), Severity = rule.Severity, Status = AlertStatus.Open.ToString(), Source = request.Source.Trim(), CauseKey = request.CauseKey.Trim(), DeduplicationKey = key, FaenaId = faena?.Id, Faena = faena, EntityType = EmptyToNull(request.EntityType), EntityId = EmptyToNull(request.EntityId), IsCriticalRepeat = rule.RepeatUntilResolved && rule.Severity == AlertSeverityLevel.Critical.ToString(), RepeatCount = 1 };
            _dbContext.Alerts.Add(alert);
        }
        else
        {
            alert.Title = request.Title.Trim(); alert.Message = request.Message.Trim(); alert.Source = request.Source.Trim(); alert.EntityType = EmptyToNull(request.EntityType); alert.EntityId = EmptyToNull(request.EntityId); alert.UpdatedAtUtc = DateTimeOffset.UtcNow;
            if (alert.IsCriticalRepeat) alert.RepeatCount++;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (rule.GenerateEmail || rule.GeneratePdf) await CreateNotificationAsync(alert, rule, request.Data, null, cancellationToken);
        await RecordAuditAsync(user, isNew ? "alert.generated" : "alert.repeated", alert.Id.ToString("D"), null, JsonSerializer.Serialize(ToAlertResponse(alert)), cancellationToken);
        return ToAlertResponse(alert);
    }

    public async Task<AlertResponse?> AcknowledgeAsync(string id, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanManage(user); var alert = await FindAlertAsync(id, cancellationToken); if (alert is null) return null; EnsureCanViewAlert(ToAlertResponse(alert), user);
        if (alert.Status != AlertStatus.Resolved.ToString()) { alert.Status = AlertStatus.Acknowledged.ToString(); alert.AcknowledgedAtUtc = DateTimeOffset.UtcNow; alert.AcknowledgedByUserId = user.UserId; alert.UpdatedAtUtc = DateTimeOffset.UtcNow; await _dbContext.SaveChangesAsync(cancellationToken); await RecordAuditAsync(user, "alert.acknowledged", id, null, null, cancellationToken); }
        return ToAlertResponse(alert);
    }

    public async Task<AlertResponse?> ResolveAsync(string id, ResolveAlertRequest request, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanManage(user); DomainGuard.AgainstEmpty(request.Reason, nameof(request.Reason)); var alert = await FindAlertAsync(id, cancellationToken); if (alert is null) return null; EnsureCanViewAlert(ToAlertResponse(alert), user);
        if (alert.Status != AlertStatus.Resolved.ToString()) { alert.Status = AlertStatus.Resolved.ToString(); alert.IsActive = false; alert.ResolvedAtUtc = DateTimeOffset.UtcNow; alert.ResolvedByUserId = user.UserId; alert.ResolutionReason = request.Reason.Trim(); alert.UpdatedAtUtc = DateTimeOffset.UtcNow; await _dbContext.SaveChangesAsync(cancellationToken); await RecordAuditAsync(user, "alert.resolved", id, null, request.Reason, cancellationToken); }
        return ToAlertResponse(alert);
    }

    public async Task<NotificationResponse?> SendTestAsync(string id, SendTestNotificationRequest request, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanManage(user); var alert = await FindAlertAsync(id, cancellationToken); if (alert is null) return null; EnsureCanViewAlert(ToAlertResponse(alert), user);
        var rule = await _dbContext.AlertRules.Include(item => item.Template).Include(item => item.Recipients).SingleAsync(item => item.Id == alert.AlertRuleId, cancellationToken);
        IReadOnlyCollection<string>? overrideRecipients = string.IsNullOrWhiteSpace(request.RecipientEmail) ? null : [request.RecipientEmail.Trim()];
        var notification = await CreateNotificationAsync(alert, rule, new Dictionary<string, string?> { ["Comments"] = request.Comments }, "test", cancellationToken, overrideRecipients);
        await RecordAuditAsync(user, "alert.test_notification_sent", id, null, notification.Id.ToString("D"), cancellationToken);
        return ToNotificationResponse(notification);
    }

    public async Task<IReadOnlyCollection<NotificationResponse>> ListNotificationsAsync(UserAccessContext user, CancellationToken cancellationToken)
    {
        var items = await _dbContext.Notifications.AsNoTracking().Include(item => item.Alert).ThenInclude(alert => alert.AlertRule).Include(item => item.Alert.Faena).Include(item => item.Recipients).Include(item => item.PdfFile)
            .OrderByDescending(item => item.CreatedAtUtc).ToArrayAsync(cancellationToken);
        return items.Where(item => CanViewAlert(ToAlertResponse(item.Alert), user) || _authorizationPolicyService.CanAdminister(user)).Select(ToNotificationResponse).ToArray();
    }

    public async Task<IReadOnlyCollection<AlertRuleResponse>> ListRulesAsync(UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanManage(user); await EnsureDefaultsAsync(cancellationToken);
        return (await _dbContext.AlertRules.AsNoTracking().Include(item => item.Template).Include(item => item.Faena).Include(item => item.Recipients).OrderBy(item => item.Name).ToArrayAsync(cancellationToken)).Select(ToRuleResponse).ToArray();
    }

    public async Task<AlertRuleResponse?> UpdateRuleAsync(string code, UpdateAlertRuleRequest request, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanConfigure(user); DomainGuard.AgainstEmpty(request.Name, nameof(request.Name)); DomainGuard.AgainstEmpty(request.EventType, nameof(request.EventType)); DomainGuard.AgainstEmpty(request.TemplateId, nameof(request.TemplateId)); DomainGuard.AgainstEmpty(request.Reason ?? string.Empty, "reason");
        var rule = await _dbContext.AlertRules.Include(item => item.Template).Include(item => item.Faena).Include(item => item.Recipients).SingleOrDefaultAsync(item => item.Code == code, cancellationToken); if (rule is null) return null;
        var template = await _dbContext.PdfTemplates.SingleOrDefaultAsync(item => item.Code == request.TemplateId, cancellationToken) ?? throw new DomainException("La plantilla PDF no existe.");
        var faena = await ResolveFaenaAsync(request.FaenaCodigo, cancellationToken);
        rule.Name = request.Name.Trim(); rule.EventType = request.EventType.Trim(); rule.IsEnabled = request.Enabled; rule.Severity = request.Severity.ToString(); rule.RepeatUntilResolved = request.RepeatUntilResolved; rule.GenerateEmail = request.GenerateEmail; rule.GeneratePdf = request.GeneratePdf; rule.TemplateId = template.Id; rule.Template = template; rule.FaenaId = faena?.Id; rule.Faena = faena; rule.UpdatedAtUtc = DateTimeOffset.UtcNow; rule.UpdatedByUserId = user.UserId;
        rule.Recipients.Clear(); foreach (var destination in SplitRecipients(request.Recipients)) rule.Recipients.Add(new AlertRuleRecipientEntity { Destination = destination, Channel = "Email", IsActive = true });
        await _dbContext.SaveChangesAsync(cancellationToken); await RecordAuditAsync(user, "alert_rule.updated", code, null, JsonSerializer.Serialize(ToRuleResponse(rule)), cancellationToken); return ToRuleResponse(rule);
    }

    private async Task<NotificationEntity> CreateNotificationAsync(AlertEntity alert, AlertRuleEntity rule, IReadOnlyDictionary<string, string?>? extraData, string? suffix, CancellationToken cancellationToken, IReadOnlyCollection<string>? overriddenRecipients = null)
    {
        var template = rule.Template ?? await _templateService.ResolveActiveAsync("alert-default", cancellationToken); var data = BuildData(alert, rule.Code, extraData); var subject = PdfTemplateService.RenderTemplate(template.SubjectTemplate, data); var body = PdfTemplateService.RenderTemplate(template.HtmlTemplate, data);
        PdfRenderResult? pdf = null; if (rule.GeneratePdf) { pdf = await _pdfService.RenderAsync(new PdfRenderRequest(template.Code, body, $"{alert.Id:D}-{suffix ?? "notification"}.pdf", data), cancellationToken); var file = await _dbContext.Files.SingleOrDefaultAsync(item => item.FileKey == pdf.FileKey, cancellationToken); if (file is not null) { alert.GeneratedPdfFileId = file.Id; } }
        var recipients = overriddenRecipients?.ToArray() ?? rule.Recipients.Where(item => item.IsActive && !string.IsNullOrWhiteSpace(item.Destination)).Select(item => item.Destination!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); if (recipients.Length == 0) recipients = ["planificacion@example.local"];
        var result = rule.GenerateEmail ? await _emailService.SendAsync(new EmailMessage(subject, body, recipients, pdf?.FileKey, pdf?.Path), cancellationToken) : new EmailSendResult(true, "None", null);
        var notification = new NotificationEntity { AlertId = alert.Id, Alert = alert, Channel = "Email", Subject = subject, Body = body, Status = result.Success ? NotificationStatus.Sent.ToString() : NotificationStatus.Failed.ToString(), SentAtUtc = result.SentAtUtc, AttemptCount = 1, Provider = result.Provider, LastError = SanitizeError(result.Error), PdfFileId = alert.GeneratedPdfFileId };
        foreach (var recipient in recipients) notification.Recipients.Add(new NotificationRecipientEntity { Destination = recipient, DeliveryStatus = result.Success ? "Sent" : "Failed" });
        notification.Attempts.Add(new NotificationAttemptEntity { AttemptNumber = 1, Success = result.Success, Provider = result.Provider, Error = SanitizeError(result.Error) }); _dbContext.Notifications.Add(notification); await _dbContext.SaveChangesAsync(cancellationToken); return notification;
    }

    private async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        await _templateService.ListAsync(cancellationToken); if (await _dbContext.AlertRules.AnyAsync(cancellationToken)) return; var template = await _dbContext.PdfTemplates.SingleAsync(item => item.Code == "alert-default", cancellationToken);
        foreach (var item in DefaultRules()) { var rule = new AlertRuleEntity { Code = item.Code, Name = item.Name, EventType = item.EventType, IsEnabled = true, Severity = item.Severity.ToString(), RepeatUntilResolved = item.Repeat, GenerateEmail = true, GeneratePdf = item.Pdf, Template = template, TemplateId = template.Id, CreatedByUserId = "system" }; foreach (var recipient in SplitRecipients(item.Recipients)) rule.Recipients.Add(new AlertRuleRecipientEntity { Destination = recipient, Channel = "Email", IsActive = true }); _dbContext.AlertRules.Add(rule); }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<(string Code, string Name, string EventType, AlertSeverityLevel Severity, bool Repeat, bool Pdf, string Recipients)> DefaultRules() =>
    [ ("document-expiring", "Documento por vencer", "DocumentoPorVencer", AlertSeverityLevel.Warning, false, true, "planificacion@example.local"), ("document-expired", "Documento vencido", "DocumentoVencido", AlertSeverityLevel.Critical, true, true, "planificacion@example.local"), ("spare-low-stock", "Repuesto bajo stock", "RepuestoBajoStock", AlertSeverityLevel.Warning, false, false, "bodega@example.local"), ("critical-spare-no-stock", "Repuesto critico sin stock", "RepuestoCriticoSinStock", AlertSeverityLevel.Critical, true, true, "bodega@example.local"), ("work-order-overdue", "OT vencida", "OTVencida", AlertSeverityLevel.Critical, true, true, "planificacion@example.local"), ("preventive-created", "Preventivo creado automaticamente", "PreventivoCreado", AlertSeverityLevel.Info, false, false, "planificacion@example.local"), ("preventive-overdue", "Preventivo vencido", "PreventivoVencido", AlertSeverityLevel.Critical, true, true, "planificacion@example.local;supervisores@example.local;jefatura.mantenimiento@example.local"), ("request-pending-approval", "Solicitud pendiente aprobacion", "SolicitudPendienteAprobacion", AlertSeverityLevel.Warning, false, false, "abastecimiento@example.local"), ("spare-pending-delivery", "Repuesto pendiente entrega", "RepuestoPendienteEntrega", AlertSeverityLevel.Warning, false, false, "bodega@example.local"), ("reserved-stock-without-pickup", "Stock reservado sin retiro", "StockReservadoSinRetiro", AlertSeverityLevel.Warning, false, false, "bodega@example.local"), ("transfer-pending", "Transferencia pendiente", "TransferenciaPendiente", AlertSeverityLevel.Warning, false, false, "bodega@example.local"), ("reception-overdue", "Recepcion vencida", "RecepcionVencida", AlertSeverityLevel.Warning, false, false, "bodega@example.local"), ("incomplete-work-order-close", "Cierre OT incompleto", "CierreOTIncompleto", AlertSeverityLevel.Critical, true, true, "planificacion@example.local"), ("availability-affected", "Disponibilidad afectada", "DisponibilidadAfectada", AlertSeverityLevel.Critical, true, true, "planificacion@example.local") ];

    private async Task<AlertEntity?> FindAlertAsync(string id, CancellationToken cancellationToken) => Guid.TryParse(id, out var parsed) ? await _dbContext.Alerts.Include(item => item.AlertRule).Include(item => item.Faena).SingleOrDefaultAsync(item => item.Id == parsed, cancellationToken) : null;
    private async Task<FaenaEntity?> ResolveFaenaAsync(string? code, CancellationToken cancellationToken) => string.IsNullOrWhiteSpace(code) ? null : await _dbContext.Faenas.SingleOrDefaultAsync(item => item.Code == code.Trim(), cancellationToken) ?? throw new DomainException("La faena indicada no existe.");
    private static void ValidateGenerate(GenerateAlertRequest request) { DomainGuard.AgainstEmpty(request.RuleCode, nameof(request.RuleCode)); DomainGuard.AgainstEmpty(request.Title, nameof(request.Title)); DomainGuard.AgainstEmpty(request.Message, nameof(request.Message)); DomainGuard.AgainstEmpty(request.Source, nameof(request.Source)); DomainGuard.AgainstEmpty(request.CauseKey, nameof(request.CauseKey)); }
    private static IReadOnlyDictionary<string, string?> BuildData(AlertEntity alert, string ruleCode, IReadOnlyDictionary<string, string?>? extra) { var data = new Dictionary<string, string?>(extra ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase) { ["AlertId"] = alert.Id.ToString("D"), ["RuleCode"] = ruleCode, ["Title"] = alert.Title, ["Message"] = alert.Message, ["Severity"] = alert.Severity, ["Status"] = alert.Status, ["Source"] = alert.Source, ["CauseKey"] = alert.CauseKey, ["FaenaCodigo"] = alert.Faena?.Code, ["EntityType"] = alert.EntityType, ["EntityId"] = alert.EntityId, ["RepeatCount"] = alert.RepeatCount.ToString(), ["CreatedAtUtc"] = alert.CreatedAtUtc.ToString("O") }; return data; }
    private static AlertResponse ToAlertResponse(AlertEntity item) => new(item.Id.ToString("D"), item.AlertRule.Code, item.Title, item.Message, Parse<AlertSeverityLevel>(item.Severity), Parse<AlertStatus>(item.Status), item.Source, item.CauseKey, item.Faena?.Code, item.EntityType, item.EntityId, item.IsCriticalRepeat, item.RepeatCount, item.CreatedAtUtc, item.UpdatedAtUtc ?? item.CreatedAtUtc, item.AcknowledgedAtUtc, item.AcknowledgedByUserId, item.ResolvedAtUtc, item.ResolvedByUserId, item.ResolutionReason);
    private static AlertRuleResponse ToRuleResponse(AlertRuleEntity item) => new(item.Code, item.Name, item.EventType, item.IsEnabled, Parse<AlertSeverityLevel>(item.Severity), item.RepeatUntilResolved, item.GenerateEmail, item.GeneratePdf, item.Template.Code, string.Join(';', item.Recipients.Where(x => x.IsActive).Select(x => x.Destination).Where(x => !string.IsNullOrWhiteSpace(x))), item.Faena?.Code);
    private static NotificationResponse ToNotificationResponse(NotificationEntity item) => new(item.Id.ToString("D"), item.AlertId.ToString("D"), item.Subject, item.Body, string.Join(';', item.Recipients.Select(x => x.Destination).Where(x => !string.IsNullOrWhiteSpace(x))), Parse<NotificationStatus>(item.Status), item.CreatedAtUtc, item.SentAtUtc, item.Provider, item.PdfFile?.FileKey, item.PdfFile?.PhysicalLocation ?? item.PdfFile?.LogicalUri, item.LastError);
    private static T Parse<T>(string value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var parsed) ? parsed : default;
    private void EnsureCanManage(UserAccessContext user) { if (!_authorizationPolicyService.CanManageAlerts(user)) throw new UnauthorizedAccessException("El usuario no tiene permiso para gestionar alertas."); }
    private void EnsureCanConfigure(UserAccessContext user) { if (!_authorizationPolicyService.CanConfigureAlerts(user)) throw new UnauthorizedAccessException("El usuario no tiene permiso para configurar alertas."); }
    private bool CanViewAlert(AlertResponse alert, UserAccessContext user) => _authorizationPolicyService.CanAdminister(user) || string.IsNullOrWhiteSpace(alert.FaenaCodigo) || _authorizationPolicyService.CanViewFaena(user, alert.FaenaCodigo);
    private void EnsureCanViewAlert(AlertResponse alert, UserAccessContext user) { if (!CanViewAlert(alert, user)) throw new UnauthorizedAccessException("El usuario no tiene acceso a la alerta solicitada."); }
    private async Task RecordAuditAsync(UserAccessContext user, string action, string entityId, string? previous, string? next, CancellationToken cancellationToken) => await _auditService.RecordAsync(new AuditEventRequest(user.UserId, action, AuditModules.Alerts, "Alert", entityId, previous, next, Severity: action.Contains("resolved", StringComparison.OrdinalIgnoreCase) ? AuditSeverity.High : AuditSeverity.Medium), cancellationToken);
    private static IReadOnlyCollection<string> SplitRecipients(string? value) => string.IsNullOrWhiteSpace(value) ? [] : value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? SanitizeError(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Length <= 2000 ? value.Trim() : value[..2000];
}