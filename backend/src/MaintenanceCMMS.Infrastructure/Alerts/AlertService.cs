using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Infrastructure.Alerts;

public sealed class AlertService : IAlertService
{
    private const string AlertRulesSchema = "alert_rules";
    private const string AlertsSchema = "alerts";
    private const string NotificationsSchema = "notifications";

    private readonly IDataProvider _dataProvider;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;
    private readonly IEmailService _emailService;
    private readonly IPdfService _pdfService;
    private readonly IPdfTemplateService _templateService;

    public AlertService(
        IDataProvider dataProvider,
        IAuditService auditService,
        IAuthorizationPolicyService authorizationPolicyService,
        IEmailService emailService,
        IPdfService pdfService,
        IPdfTemplateService templateService)
    {
        _dataProvider = dataProvider;
        _auditService = auditService;
        _authorizationPolicyService = authorizationPolicyService;
        _emailService = emailService;
        _pdfService = pdfService;
        _templateService = templateService;
    }

    public async Task<IReadOnlyCollection<AlertResponse>> ListAsync(
        AlertQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(AlertsSchema, cancellationToken);
        return rows
            .Select(ToAlertResponse)
            .Where(alert => CanViewAlert(alert, user))
            .Where(alert => query.IncludeResolved || alert.Status != AlertStatus.Resolved)
            .Where(alert => !query.Status.HasValue || alert.Status == query.Status.Value)
            .Where(alert => !query.Severity.HasValue || alert.Severity == query.Severity.Value)
            .Where(alert => string.IsNullOrWhiteSpace(query.Source) || alert.Source.Equals(query.Source, StringComparison.OrdinalIgnoreCase))
            .Where(alert => string.IsNullOrWhiteSpace(query.FaenaCodigo) || SameCode(alert.FaenaCodigo, query.FaenaCodigo))
            .OrderByDescending(alert => alert.Severity)
            .ThenByDescending(alert => alert.UpdatedAtUtc)
            .ToArray();
    }

    public async Task<AlertResponse> GenerateAsync(
        GenerateAlertRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        DomainGuard.AgainstEmpty(request.RuleCode, nameof(request.RuleCode));
        DomainGuard.AgainstEmpty(request.Title, nameof(request.Title));
        DomainGuard.AgainstEmpty(request.Message, nameof(request.Message));
        DomainGuard.AgainstEmpty(request.Source, nameof(request.Source));
        DomainGuard.AgainstEmpty(request.CauseKey, nameof(request.CauseKey));

        var rules = await EnsureRulesAsync(cancellationToken);
        var rule = rules.Select(ToRuleResponse).FirstOrDefault(item => SameCode(item.Code, request.RuleCode));
        if (rule is null || !rule.Enabled)
        {
            throw new DomainException($"La regla de alerta '{request.RuleCode}' no existe o no esta habilitada.");
        }

        var rows = (await _dataProvider.ReadRowsAsync(AlertsSchema, cancellationToken)).ToList();
        var existingIndex = FindOpenByCause(rows, request.RuleCode, request.CauseKey);
        DataRow row;
        DataRow? previous = null;
        if (existingIndex >= 0)
        {
            previous = rows[existingIndex];
            var current = ToAlertResponse(previous);
            var nextRepeat = rule.RepeatUntilResolved && rule.Severity == AlertSeverityLevel.Critical
                ? current.RepeatCount + 1
                : current.RepeatCount;
            row = AlertRow(
                current.AlertId,
                current.RuleCode,
                request.Title,
                request.Message,
                rule.Severity,
                current.Status,
                request.Source,
                request.CauseKey,
                request.FaenaCodigo,
                request.EntityType,
                request.EntityId,
                rule.RepeatUntilResolved && rule.Severity == AlertSeverityLevel.Critical,
                nextRepeat,
                current.CreatedAtUtc,
                DateTimeOffset.UtcNow,
                current.AcknowledgedAtUtc,
                current.AcknowledgedBy,
                current.ResolvedAtUtc,
                current.ResolvedBy,
                current.ResolutionReason);
            rows[existingIndex] = row;
        }
        else
        {
            row = AlertRow(
                Guid.NewGuid().ToString("D"),
                rule.Code,
                request.Title,
                request.Message,
                rule.Severity,
                AlertStatus.Open,
                request.Source,
                request.CauseKey,
                request.FaenaCodigo,
                request.EntityType,
                request.EntityId,
                rule.RepeatUntilResolved && rule.Severity == AlertSeverityLevel.Critical,
                1,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                null,
                null);
            rows.Add(row);
        }

        await _dataProvider.SaveRowsAsync(AlertsSchema, rows, cancellationToken);
        var alert = ToAlertResponse(row);
        if (rule.GenerateEmail || rule.GeneratePdf)
        {
            await CreateNotificationAsync(alert, rule, request.Data, null, cancellationToken);
        }

        await RecordAuditAsync(
            user,
            previous is null ? "alert.generated" : "alert.repeated",
            alert.AlertId,
            previous is null ? null : Serialize(previous),
            Serialize(row),
            cancellationToken);

        return alert;
    }

    public async Task<AlertResponse?> AcknowledgeAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        var rows = (await _dataProvider.ReadRowsAsync(AlertsSchema, cancellationToken)).ToList();
        var index = FindIndex(rows, id);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var current = ToAlertResponse(existing);
        EnsureCanViewAlert(current, user);
        if (current.Status == AlertStatus.Resolved)
        {
            return current;
        }

        var updated = WithValues(existing, new Dictionary<string, string?>
        {
            ["Status"] = AlertStatus.Acknowledged.ToString(),
            ["AcknowledgedAtUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            ["AcknowledgedBy"] = user.UserId,
            ["UpdatedAtUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        });

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(AlertsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "alert.acknowledged", id, Serialize(existing), Serialize(updated), cancellationToken);
        return ToAlertResponse(updated);
    }

    public async Task<AlertResponse?> ResolveAsync(
        string id,
        ResolveAlertRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        DomainGuard.AgainstEmpty(request.Reason, nameof(request.Reason));
        var rows = (await _dataProvider.ReadRowsAsync(AlertsSchema, cancellationToken)).ToList();
        var index = FindIndex(rows, id);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var current = ToAlertResponse(existing);
        EnsureCanViewAlert(current, user);
        var updated = WithValues(existing, new Dictionary<string, string?>
        {
            ["Status"] = AlertStatus.Resolved.ToString(),
            ["ResolvedAtUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            ["ResolvedBy"] = user.UserId,
            ["ResolutionReason"] = request.Reason,
            ["UpdatedAtUtc"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        });

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(AlertsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "alert.resolved", id, Serialize(existing), Serialize(updated), cancellationToken);
        return ToAlertResponse(updated);
    }

    public async Task<NotificationResponse?> SendTestAsync(
        string id,
        SendTestNotificationRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        var alerts = await _dataProvider.ReadRowsAsync(AlertsSchema, cancellationToken);
        var alert = alerts.Select(ToAlertResponse).FirstOrDefault(item => SameCode(item.AlertId, id));
        if (alert is null)
        {
            return null;
        }

        EnsureCanViewAlert(alert, user);
        var rule = (await EnsureRulesAsync(cancellationToken))
            .Select(ToRuleResponse)
            .FirstOrDefault(item => SameCode(item.Code, alert.RuleCode));
        if (rule is null)
        {
            throw new DomainException("La alerta no tiene regla asociada.");
        }

        var notification = await CreateNotificationAsync(
            alert,
            rule with
            {
                GenerateEmail = true,
                GeneratePdf = rule.GeneratePdf,
                Recipients = request.RecipientEmail ?? rule.Recipients
            },
            new Dictionary<string, string?>
            {
                ["Comments"] = request.Comments
            },
            "test",
            cancellationToken);

        await RecordAuditAsync(user, "alert.test_notification_sent", alert.AlertId, null, notification.NotificationId, cancellationToken);
        return notification;
    }

    public async Task<IReadOnlyCollection<NotificationResponse>> ListNotificationsAsync(
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var alerts = (await _dataProvider.ReadRowsAsync(AlertsSchema, cancellationToken))
            .Select(ToAlertResponse)
            .Where(alert => CanViewAlert(alert, user))
            .Select(alert => alert.AlertId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var notifications = await _dataProvider.ReadRowsAsync(NotificationsSchema, cancellationToken);
        return notifications
            .Select(ToNotificationResponse)
            .Where(notification => alerts.Contains(notification.AlertId) || _authorizationPolicyService.CanAdminister(user))
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<AlertRuleResponse>> ListRulesAsync(
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        var rows = await EnsureRulesAsync(cancellationToken);
        return rows.Select(ToRuleResponse).OrderBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<AlertRuleResponse?> UpdateRuleAsync(
        string code,
        UpdateAlertRuleRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanConfigure(user);
        DomainGuard.AgainstEmpty(request.Name, nameof(request.Name));
        DomainGuard.AgainstEmpty(request.EventType, nameof(request.EventType));
        DomainGuard.AgainstEmpty(request.TemplateId, nameof(request.TemplateId));
        DomainGuard.AgainstEmpty(request.Reason ?? string.Empty, "reason");

        var rows = (await EnsureRulesAsync(cancellationToken)).ToList();
        var index = FindRuleIndex(rows, code);
        if (index < 0)
        {
            return null;
        }

        var existing = rows[index];
        var updated = RuleRow(
            existing.GetValue("Code") ?? code,
            request.Name,
            request.EventType,
            request.Enabled,
            request.Severity,
            request.RepeatUntilResolved,
            request.GenerateEmail,
            request.GeneratePdf,
            request.TemplateId,
            request.Recipients,
            request.FaenaCodigo);

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(AlertRulesSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "alert_rule.updated", code, Serialize(existing), Serialize(updated), cancellationToken);

        return ToRuleResponse(updated);
    }

    private async Task<NotificationResponse> CreateNotificationAsync(
        AlertResponse alert,
        AlertRuleResponse rule,
        IReadOnlyDictionary<string, string?>? extraData,
        string? suffix,
        CancellationToken cancellationToken)
    {
        var templates = await _templateService.ListAsync(cancellationToken);
        var template = templates.FirstOrDefault(item => SameCode(item.TemplateId, rule.TemplateId)) ??
                       templates.First(item => item.Active);
        var data = BuildTemplateData(alert, extraData);
        var subject = PdfTemplateService.RenderTemplate(template.SubjectTemplate, data);
        var body = PdfTemplateService.RenderTemplate(template.HtmlTemplate, data);
        PdfRenderResult? pdf = null;

        if (rule.GeneratePdf)
        {
            pdf = await _pdfService.RenderAsync(new PdfRenderRequest(
                template.TemplateId,
                body,
                $"{alert.AlertId}-{suffix ?? "notification"}.pdf",
                data), cancellationToken);
        }

        var recipients = SplitRecipients(rule.Recipients);
        if (recipients.Count == 0)
        {
            recipients = ["planificacion@example.local"];
        }

        var emailResult = rule.GenerateEmail
            ? await _emailService.SendAsync(new EmailMessage(subject, body, recipients, pdf?.FileKey, pdf?.Path), cancellationToken)
            : new EmailSendResult(true, "None", null);

        var row = NotificationRow(
            Guid.NewGuid().ToString("D"),
            alert.AlertId,
            subject,
            body,
            string.Join(';', recipients),
            emailResult.Success ? NotificationStatus.Sent : NotificationStatus.Failed,
            DateTimeOffset.UtcNow,
            emailResult.SentAtUtc,
            emailResult.Provider,
            pdf?.FileKey,
            pdf?.Path,
            emailResult.Error);

        var rows = (await _dataProvider.ReadRowsAsync(NotificationsSchema, cancellationToken)).ToList();
        rows.Add(row);
        await _dataProvider.SaveRowsAsync(NotificationsSchema, rows, cancellationToken);
        return ToNotificationResponse(row);
    }

    private async Task<IReadOnlyCollection<DataRow>> EnsureRulesAsync(CancellationToken cancellationToken)
    {
        var rows = (await _dataProvider.ReadRowsAsync(AlertRulesSchema, cancellationToken)).ToList();
        if (rows.Count > 0)
        {
            return rows;
        }

        rows.AddRange(DefaultRules());
        await _dataProvider.SaveRowsAsync(AlertRulesSchema, rows, cancellationToken);
        await _templateService.ListAsync(cancellationToken);
        return rows;
    }

    private static IReadOnlyCollection<DataRow> DefaultRules()
    {
        return
        [
            RuleRow("document-expiring", "Documento por vencer", "DocumentoPorVencer", true, AlertSeverityLevel.Warning, false, true, true, "alert-default", "planificacion@example.local", null),
            RuleRow("document-expired", "Documento vencido", "DocumentoVencido", true, AlertSeverityLevel.Critical, true, true, true, "alert-default", "planificacion@example.local", null),
            RuleRow("spare-low-stock", "Repuesto bajo stock", "RepuestoBajoStock", true, AlertSeverityLevel.Warning, false, true, false, "alert-default", "bodega@example.local", null),
            RuleRow("critical-spare-no-stock", "Repuesto critico sin stock", "RepuestoCriticoSinStock", true, AlertSeverityLevel.Critical, true, true, true, "alert-default", "bodega@example.local", null),
            RuleRow("work-order-overdue", "OT vencida", "OTVencida", true, AlertSeverityLevel.Critical, true, true, true, "alert-default", "planificacion@example.local", null),
            RuleRow("preventive-created", "Preventivo creado automaticamente", "PreventivoCreado", true, AlertSeverityLevel.Info, false, true, false, "alert-default", "planificacion@example.local", null),
            RuleRow("request-pending-approval", "Solicitud pendiente aprobacion", "SolicitudPendienteAprobacion", true, AlertSeverityLevel.Warning, false, true, false, "alert-default", "abastecimiento@example.local", null),
            RuleRow("spare-pending-delivery", "Repuesto pendiente entrega", "RepuestoPendienteEntrega", true, AlertSeverityLevel.Warning, false, true, false, "alert-default", "bodega@example.local", null),
            RuleRow("reserved-stock-without-pickup", "Stock reservado sin retiro", "StockReservadoSinRetiro", true, AlertSeverityLevel.Warning, false, true, false, "alert-default", "bodega@example.local", null),
            RuleRow("transfer-pending", "Transferencia pendiente", "TransferenciaPendiente", true, AlertSeverityLevel.Warning, false, true, false, "alert-default", "bodega@example.local", null),
            RuleRow("reception-overdue", "Recepcion vencida", "RecepcionVencida", true, AlertSeverityLevel.Warning, false, true, false, "alert-default", "bodega@example.local", null),
            RuleRow("incomplete-work-order-close", "Cierre OT incompleto", "CierreOTIncompleto", true, AlertSeverityLevel.Critical, true, true, true, "alert-default", "planificacion@example.local", null),
            RuleRow("availability-affected", "Disponibilidad afectada", "DisponibilidadAfectada", true, AlertSeverityLevel.Critical, true, true, true, "alert-default", "planificacion@example.local", null)
        ];
    }

    private static DataRow RuleRow(
        string code,
        string name,
        string eventType,
        bool enabled,
        AlertSeverityLevel severity,
        bool repeatUntilResolved,
        bool generateEmail,
        bool generatePdf,
        string templateId,
        string recipients,
        string? faenaCodigo)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = code,
            ["Name"] = name,
            ["EventType"] = eventType,
            ["Enabled"] = enabled ? "true" : "false",
            ["Severity"] = severity.ToString(),
            ["RepeatUntilResolved"] = repeatUntilResolved ? "true" : "false",
            ["GenerateEmail"] = generateEmail ? "true" : "false",
            ["GeneratePdf"] = generatePdf ? "true" : "false",
            ["TemplateId"] = templateId,
            ["Recipients"] = recipients,
            ["FaenaCodigo"] = EmptyToNull(faenaCodigo)
        });
    }

    private static DataRow AlertRow(
        string alertId,
        string ruleCode,
        string title,
        string message,
        AlertSeverityLevel severity,
        AlertStatus status,
        string source,
        string causeKey,
        string? faenaCodigo,
        string? entityType,
        string? entityId,
        bool isCriticalRepeat,
        int repeatCount,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? acknowledgedAtUtc,
        string? acknowledgedBy,
        DateTimeOffset? resolvedAtUtc,
        string? resolvedBy,
        string? resolutionReason)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["AlertId"] = alertId,
            ["RuleCode"] = ruleCode,
            ["Title"] = title,
            ["Message"] = message,
            ["Severity"] = severity.ToString(),
            ["Status"] = status.ToString(),
            ["Source"] = source,
            ["CauseKey"] = causeKey,
            ["FaenaCodigo"] = EmptyToNull(faenaCodigo),
            ["EntityType"] = EmptyToNull(entityType),
            ["EntityId"] = EmptyToNull(entityId),
            ["IsCriticalRepeat"] = isCriticalRepeat ? "true" : "false",
            ["RepeatCount"] = repeatCount.ToString(CultureInfo.InvariantCulture),
            ["CreatedAtUtc"] = createdAtUtc.UtcDateTime.ToString("O"),
            ["UpdatedAtUtc"] = updatedAtUtc.UtcDateTime.ToString("O"),
            ["AcknowledgedAtUtc"] = FormatDateTime(acknowledgedAtUtc),
            ["AcknowledgedBy"] = EmptyToNull(acknowledgedBy),
            ["ResolvedAtUtc"] = FormatDateTime(resolvedAtUtc),
            ["ResolvedBy"] = EmptyToNull(resolvedBy),
            ["ResolutionReason"] = EmptyToNull(resolutionReason)
        });
    }

    private static DataRow NotificationRow(
        string notificationId,
        string alertId,
        string subject,
        string body,
        string recipients,
        NotificationStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? sentAtUtc,
        string? provider,
        string? pdfFileKey,
        string? pdfPath,
        string? error)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["NotificationId"] = notificationId,
            ["AlertId"] = alertId,
            ["Subject"] = subject,
            ["Body"] = body,
            ["Recipients"] = recipients,
            ["Status"] = status.ToString(),
            ["CreatedAtUtc"] = createdAtUtc.UtcDateTime.ToString("O"),
            ["SentAtUtc"] = FormatDateTime(sentAtUtc),
            ["Provider"] = EmptyToNull(provider),
            ["PdfFileKey"] = EmptyToNull(pdfFileKey),
            ["PdfPath"] = EmptyToNull(pdfPath),
            ["Error"] = EmptyToNull(error)
        });
    }

    private static AlertResponse ToAlertResponse(DataRow row)
    {
        return new AlertResponse(
            row.GetValue("AlertId")?.Trim() ?? string.Empty,
            row.GetValue("RuleCode")?.Trim() ?? string.Empty,
            row.GetValue("Title")?.Trim() ?? string.Empty,
            row.GetValue("Message")?.Trim() ?? string.Empty,
            ParseEnum(row.GetValue("Severity"), AlertSeverityLevel.Info),
            ParseEnum(row.GetValue("Status"), AlertStatus.Open),
            row.GetValue("Source")?.Trim() ?? string.Empty,
            row.GetValue("CauseKey")?.Trim() ?? string.Empty,
            EmptyToNull(row.GetValue("FaenaCodigo")),
            EmptyToNull(row.GetValue("EntityType")),
            EmptyToNull(row.GetValue("EntityId")),
            ParseBool(row.GetValue("IsCriticalRepeat")),
            ParseInt(row.GetValue("RepeatCount"), 1),
            ParseDateTime(row.GetValue("CreatedAtUtc")) ?? DateTimeOffset.UtcNow,
            ParseDateTime(row.GetValue("UpdatedAtUtc")) ?? DateTimeOffset.UtcNow,
            ParseDateTime(row.GetValue("AcknowledgedAtUtc")),
            EmptyToNull(row.GetValue("AcknowledgedBy")),
            ParseDateTime(row.GetValue("ResolvedAtUtc")),
            EmptyToNull(row.GetValue("ResolvedBy")),
            EmptyToNull(row.GetValue("ResolutionReason")));
    }

    private static AlertRuleResponse ToRuleResponse(DataRow row)
    {
        return new AlertRuleResponse(
            row.GetValue("Code")?.Trim() ?? string.Empty,
            row.GetValue("Name")?.Trim() ?? string.Empty,
            row.GetValue("EventType")?.Trim() ?? string.Empty,
            ParseBool(row.GetValue("Enabled"), true),
            ParseEnum(row.GetValue("Severity"), AlertSeverityLevel.Warning),
            ParseBool(row.GetValue("RepeatUntilResolved")),
            ParseBool(row.GetValue("GenerateEmail"), true),
            ParseBool(row.GetValue("GeneratePdf")),
            row.GetValue("TemplateId")?.Trim() ?? "alert-default",
            row.GetValue("Recipients")?.Trim() ?? string.Empty,
            EmptyToNull(row.GetValue("FaenaCodigo")));
    }

    private static NotificationResponse ToNotificationResponse(DataRow row)
    {
        return new NotificationResponse(
            row.GetValue("NotificationId")?.Trim() ?? string.Empty,
            row.GetValue("AlertId")?.Trim() ?? string.Empty,
            row.GetValue("Subject")?.Trim() ?? string.Empty,
            row.GetValue("Body")?.Trim() ?? string.Empty,
            row.GetValue("Recipients")?.Trim() ?? string.Empty,
            ParseEnum(row.GetValue("Status"), NotificationStatus.Pending),
            ParseDateTime(row.GetValue("CreatedAtUtc")) ?? DateTimeOffset.UtcNow,
            ParseDateTime(row.GetValue("SentAtUtc")),
            EmptyToNull(row.GetValue("Provider")),
            EmptyToNull(row.GetValue("PdfFileKey")),
            EmptyToNull(row.GetValue("PdfPath")),
            EmptyToNull(row.GetValue("Error")));
    }

    private static IReadOnlyDictionary<string, string?> BuildTemplateData(
        AlertResponse alert,
        IReadOnlyDictionary<string, string?>? extraData)
    {
        var data = new Dictionary<string, string?>(extraData ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase)
        {
            ["AlertId"] = alert.AlertId,
            ["RuleCode"] = alert.RuleCode,
            ["Title"] = alert.Title,
            ["Message"] = alert.Message,
            ["Severity"] = alert.Severity.ToString(),
            ["Status"] = alert.Status.ToString(),
            ["Source"] = alert.Source,
            ["CauseKey"] = alert.CauseKey,
            ["FaenaCodigo"] = alert.FaenaCodigo,
            ["EntityType"] = alert.EntityType,
            ["EntityId"] = alert.EntityId,
            ["RepeatCount"] = alert.RepeatCount.ToString(CultureInfo.InvariantCulture),
            ["CreatedAtUtc"] = alert.CreatedAtUtc.ToString("O")
        };

        return data;
    }

    private void EnsureCanManage(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanManageAlerts(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para gestionar alertas.");
        }
    }

    private void EnsureCanConfigure(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanConfigureAlerts(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para configurar alertas.");
        }
    }

    private bool CanViewAlert(AlertResponse alert, UserAccessContext user)
    {
        return _authorizationPolicyService.CanAdminister(user) ||
               string.IsNullOrWhiteSpace(alert.FaenaCodigo) ||
               _authorizationPolicyService.CanViewFaena(user, alert.FaenaCodigo);
    }

    private void EnsureCanViewAlert(AlertResponse alert, UserAccessContext user)
    {
        if (!CanViewAlert(alert, user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la alerta solicitada.");
        }
    }

    private async Task RecordAuditAsync(
        UserAccessContext user,
        string action,
        string entityId,
        string? previousValue,
        string? newValue,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            AuditModules.Alerts,
            "Alert",
            entityId,
            previousValue,
            newValue,
            Severity: action.Contains("resolved", StringComparison.OrdinalIgnoreCase)
                ? AuditSeverity.High
                : AuditSeverity.Medium), cancellationToken);
    }

    private static int FindOpenByCause(IReadOnlyList<DataRow> rows, string ruleCode, string causeKey)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var alert = ToAlertResponse(rows[index]);
            if (SameCode(alert.RuleCode, ruleCode) &&
                SameCode(alert.CauseKey, causeKey) &&
                alert.Status != AlertStatus.Resolved)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindIndex(IReadOnlyList<DataRow> rows, string id)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (SameCode(rows[index].GetValue("AlertId"), id))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindRuleIndex(IReadOnlyList<DataRow> rows, string code)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (SameCode(rows[index].GetValue("Code"), code))
            {
                return index;
            }
        }

        return -1;
    }

    private static DataRow WithValues(DataRow row, IReadOnlyDictionary<string, string?> nextValues)
    {
        var values = new Dictionary<string, string?>(row.Values, StringComparer.OrdinalIgnoreCase);
        foreach (var value in nextValues)
        {
            values[value.Key] = value.Value;
        }

        return new DataRow(values);
    }

    private static string Serialize(DataRow row)
    {
        return JsonSerializer.Serialize(row.Values);
    }

    private static IReadOnlyCollection<string> SplitRecipients(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? FormatDateTime(DateTimeOffset? value)
    {
        return value?.UtcDateTime.ToString("O");
    }

    private static DateTimeOffset? ParseDateTime(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;
    }

    private static bool ParseBool(string? value, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("si", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum defaultValue)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) ? result : defaultValue;
    }

    private static bool SameCode(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
