using System.Globalization;
using ClosedXML.Excel;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Alerts;

public sealed class AlertsExcelImportService : IAlertsExcelImportService
{
    private readonly CmmsDbContext _dbContext;

    public AlertsExcelImportService(CmmsDbContext dbContext) => _dbContext = dbContext;

    public async Task<AlertsExcelImportResult> ImportAsync(AlertsExcelImportRequest request, CancellationToken cancellationToken)
    {
        ValidatePaths(request);
        var templates = ReadRows(request.PdfTemplatesPath);
        var rules = ReadRows(request.AlertRulesPath);
        var alerts = ReadRows(request.AlertsPath);
        var notifications = ReadRows(request.NotificationsPath);
        var state = new ImportState();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ImportTemplatesAsync(templates, state.PdfTemplates, cancellationToken);
        if (state.HasErrors) return await RollbackAsync(transaction, state, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await ImportRulesAsync(rules, state.AlertRules, cancellationToken);
        if (state.HasErrors) return await RollbackAsync(transaction, state, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await ImportAlertsAsync(alerts, state.Alerts, cancellationToken);
        if (state.HasErrors) return await RollbackAsync(transaction, state, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await ImportNotificationsAsync(notifications, state.Notifications, cancellationToken);
        if (state.HasErrors) return await RollbackAsync(transaction, state, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return state.ToResult();
    }

    private async Task<AlertsExcelImportResult> RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        ImportState state,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        return state.ToResult();
    }
    private async Task ImportTemplatesAsync(IReadOnlyCollection<Row> rows, Summary summary, CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            summary.RowsRead++;
            var code = Required(row, "TemplateId", summary);
            if (code is null) continue;
            if (!seen.Add(code)) { summary.Duplicates++; summary.Warnings.Add($"Fila {row.Number}: TemplateId duplicado '{code}'."); continue; }
            var name = Required(row, "Name", summary); var eventType = Required(row, "EventType", summary); var subject = Required(row, "SubjectTemplate", summary); var html = Required(row, "HtmlTemplate", summary);
            if (name is null || eventType is null || subject is null || html is null) continue;
            if (!PdfTemplateService.PlaceholdersAreValid(subject) || !PdfTemplateService.PlaceholdersAreValid(html)) { summary.Errors++; summary.Warnings.Add($"Fila {row.Number}: placeholders invalidos."); continue; }
            var active = Bool(row, "Active", true);
            var entity = await _dbContext.PdfTemplates.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
            if (entity is null) { _dbContext.PdfTemplates.Add(new PdfTemplateEntity { Code = code, Name = name, EventType = eventType, SubjectTemplate = subject, HtmlTemplate = html, IsActive = active, TemplateVersion = 1, CreatedByUserId = "excel-import" }); summary.Inserted++; }
            else if (entity.Name == name && entity.EventType == eventType && entity.SubjectTemplate == subject && entity.HtmlTemplate == html && entity.IsActive == active) summary.Skipped++;
            else { entity.Name = name; entity.EventType = eventType; entity.SubjectTemplate = subject; entity.HtmlTemplate = html; entity.IsActive = active; entity.TemplateVersion++; entity.UpdatedAtUtc = DateTimeOffset.UtcNow; entity.UpdatedByUserId = "excel-import"; summary.Updated++; }
        }
    }

    private async Task ImportRulesAsync(IReadOnlyCollection<Row> rows, Summary summary, CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            summary.RowsRead++;
            var code = Required(row, "Code", summary); var name = Required(row, "Name", summary); var eventType = Required(row, "EventType", summary); var templateCode = Required(row, "TemplateId", summary);
            if (code is null || name is null || eventType is null || templateCode is null) continue;
            if (!seen.Add(code)) { summary.Duplicates++; continue; }
            if (!Enum.TryParse<AlertSeverityLevel>(Value(row, "Severity"), true, out var severity)) { summary.Errors++; summary.Warnings.Add($"Fila {row.Number}: Severity invalida."); continue; }
            var template = await _dbContext.PdfTemplates.SingleOrDefaultAsync(item => item.Code == templateCode, cancellationToken);
            if (template is null) { summary.ReferencesNotFound.Add($"Fila {row.Number}: plantilla '{templateCode}' no existe."); continue; }
            var faenaCode = Value(row, "FaenaCodigo"); var faena = string.IsNullOrWhiteSpace(faenaCode) ? null : await _dbContext.Faenas.SingleOrDefaultAsync(item => item.Code == faenaCode, cancellationToken);
            if (!string.IsNullOrWhiteSpace(faenaCode) && faena is null) { summary.ReferencesNotFound.Add($"Fila {row.Number}: faena '{faenaCode}' no existe."); continue; }
            var recipients = Split(Value(row, "Recipients"));
            var entity = await _dbContext.AlertRules.Include(item => item.Recipients).SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
            if (entity is null)
            {
                entity = new AlertRuleEntity { Code = code, Name = name, EventType = eventType, IsEnabled = Bool(row, "Enabled", true), Severity = severity.ToString(), RepeatUntilResolved = Bool(row, "RepeatUntilResolved"), GenerateEmail = Bool(row, "GenerateEmail", true), GeneratePdf = Bool(row, "GeneratePdf"), Template = template, TemplateId = template.Id, Faena = faena, FaenaId = faena?.Id, CreatedByUserId = "excel-import" };
                foreach (var recipient in recipients) entity.Recipients.Add(new AlertRuleRecipientEntity { Destination = recipient, Channel = "Email", IsActive = true });
                _dbContext.AlertRules.Add(entity); summary.Inserted++;
            }
            else
            {
                var same = entity.Name == name && entity.EventType == eventType && entity.IsEnabled == Bool(row, "Enabled", true) && entity.Severity == severity.ToString() && entity.RepeatUntilResolved == Bool(row, "RepeatUntilResolved") && entity.GenerateEmail == Bool(row, "GenerateEmail", true) && entity.GeneratePdf == Bool(row, "GeneratePdf") && entity.TemplateId == template.Id && entity.FaenaId == faena?.Id && entity.Recipients.Where(x => x.IsActive).Select(x => x.Destination).Order().SequenceEqual(recipients.Order(), StringComparer.OrdinalIgnoreCase);
                if (same) summary.Skipped++;
                else { entity.Name = name; entity.EventType = eventType; entity.IsEnabled = Bool(row, "Enabled", true); entity.Severity = severity.ToString(); entity.RepeatUntilResolved = Bool(row, "RepeatUntilResolved"); entity.GenerateEmail = Bool(row, "GenerateEmail", true); entity.GeneratePdf = Bool(row, "GeneratePdf"); entity.Template = template; entity.TemplateId = template.Id; entity.Faena = faena; entity.FaenaId = faena?.Id; entity.Recipients.Clear(); foreach (var recipient in recipients) entity.Recipients.Add(new AlertRuleRecipientEntity { Destination = recipient, Channel = "Email", IsActive = true }); entity.UpdatedAtUtc = DateTimeOffset.UtcNow; summary.Updated++; }
            }
        }
    }

    private async Task ImportAlertsAsync(IReadOnlyCollection<Row> rows, Summary summary, CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            summary.RowsRead++;
            if (!Guid.TryParse(Value(row, "AlertId"), out var id)) { summary.Errors++; summary.Warnings.Add($"Fila {row.Number}: AlertId invalido."); continue; }
            var ruleCode = Required(row, "RuleCode", summary); if (ruleCode is null) continue;
            var rule = await _dbContext.AlertRules.SingleOrDefaultAsync(item => item.Code == ruleCode, cancellationToken);
            if (rule is null) { summary.ReferencesNotFound.Add($"Fila {row.Number}: regla '{ruleCode}' no existe."); continue; }
            if (!Enum.TryParse<AlertSeverityLevel>(Value(row, "Severity"), true, out var severity) || !Enum.TryParse<AlertStatus>(Value(row, "Status"), true, out var status)) { summary.Errors++; summary.Warnings.Add($"Fila {row.Number}: estado o severidad invalida."); continue; }
            var entity = await _dbContext.Alerts.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity is null) { _dbContext.Alerts.Add(new AlertEntity { Id = id, AlertRule = rule, AlertRuleId = rule.Id, Title = Required(row, "Title", summary) ?? string.Empty, Message = Required(row, "Message", summary) ?? string.Empty, Severity = severity.ToString(), Status = status.ToString(), Source = Required(row, "Source", summary) ?? string.Empty, CauseKey = Required(row, "CauseKey", summary) ?? string.Empty, DeduplicationKey = $"{rule.Code}:{Value(row, "CauseKey")}", EntityType = Null(Value(row, "EntityType")), EntityId = Null(Value(row, "EntityId")), RepeatCount = Int(row, "RepeatCount", 1), IsActive = status != AlertStatus.Resolved, CreatedAtUtc = Date(row, "CreatedAtUtc") ?? DateTimeOffset.UtcNow }); summary.Inserted++; }
            else summary.Skipped++;
        }
    }

    private async Task ImportNotificationsAsync(IReadOnlyCollection<Row> rows, Summary summary, CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            summary.RowsRead++;
            if (!Guid.TryParse(Value(row, "NotificationId"), out var id) || !Guid.TryParse(Value(row, "AlertId"), out var alertId)) { summary.Errors++; summary.Warnings.Add($"Fila {row.Number}: identificador invalido."); continue; }
            var alert = await _dbContext.Alerts.SingleOrDefaultAsync(item => item.Id == alertId, cancellationToken);
            if (alert is null) { summary.ReferencesNotFound.Add($"Fila {row.Number}: alerta '{alertId}' no existe."); continue; }
            if (!Enum.TryParse<NotificationStatus>(Value(row, "Status"), true, out var status)) { summary.Errors++; summary.Warnings.Add($"Fila {row.Number}: estado invalido."); continue; }
            if (await _dbContext.Notifications.AnyAsync(item => item.Id == id, cancellationToken)) { summary.Skipped++; continue; }
            var entity = new NotificationEntity { Id = id, Alert = alert, AlertId = alertId, Channel = "Email", Subject = Required(row, "Subject", summary) ?? string.Empty, Body = Required(row, "Body", summary) ?? string.Empty, Status = status.ToString(), SentAtUtc = Date(row, "SentAtUtc"), Provider = Null(Value(row, "Provider")), LastError = Null(Value(row, "Error")), CreatedAtUtc = Date(row, "CreatedAtUtc") ?? DateTimeOffset.UtcNow, AttemptCount = status == NotificationStatus.Pending ? 0 : 1 };
            foreach (var recipient in Split(Value(row, "Recipients"))) entity.Recipients.Add(new NotificationRecipientEntity { Destination = recipient, DeliveryStatus = status == NotificationStatus.Sent ? "Sent" : status.ToString() });
            if (entity.AttemptCount > 0) entity.Attempts.Add(new NotificationAttemptEntity { AttemptNumber = 1, Success = status == NotificationStatus.Sent, Provider = entity.Provider, Error = entity.LastError });
            _dbContext.Notifications.Add(entity); summary.Inserted++;
        }
    }

    private static IReadOnlyCollection<Row> ReadRows(string path)
    {
        using var workbook = new XLWorkbook(path); var sheet = workbook.Worksheets.First(); var header = sheet.FirstRowUsed() ?? throw new DomainException("El Excel no contiene encabezados."); var columns = header.CellsUsed().ToDictionary(cell => cell.GetString().Trim(), cell => cell.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
        return sheet.RowsUsed().Skip(1).Select(row => new Row(row.RowNumber(), columns.ToDictionary(pair => pair.Key, pair => row.Cell(pair.Value).GetString().Trim(), StringComparer.OrdinalIgnoreCase))).ToArray();
    }

    private static void ValidatePaths(AlertsExcelImportRequest request) { foreach (var path in new[] { request.PdfTemplatesPath, request.AlertRulesPath, request.AlertsPath, request.NotificationsPath }) if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new DomainException("No se encontro uno de los Excel de alertas."); }
    private static string Value(Row row, string name) => row.Values.TryGetValue(name, out var value) ? value : string.Empty;
    private static string? Required(Row row, string name, Summary summary) { var value = Value(row, name); if (!string.IsNullOrWhiteSpace(value)) return value; summary.Errors++; summary.Warnings.Add($"Fila {row.Number}: {name} es obligatorio."); return null; }
    private static bool Bool(Row row, string name, bool fallback = false) => bool.TryParse(Value(row, name), out var value) ? value : fallback;
    private static int Int(Row row, string name, int fallback) => int.TryParse(Value(row, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static DateTimeOffset? Date(Row row, string name) => DateTimeOffset.TryParse(Value(row, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static IReadOnlyCollection<string> Split(string value) => string.IsNullOrWhiteSpace(value) ? [] : value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private sealed record Row(int Number, IReadOnlyDictionary<string, string> Values);
    private sealed class Summary { public int RowsRead; public int Inserted; public int Updated; public int Skipped; public int Duplicates; public int Errors; public List<string> Warnings { get; } = []; public List<string> ReferencesNotFound { get; } = []; public AlertsExcelImportSummary Result() => new(RowsRead, Inserted, Updated, Skipped, Duplicates, Errors + ReferencesNotFound.Count, Warnings, ReferencesNotFound); }
    private sealed class ImportState { public Summary PdfTemplates { get; } = new(); public Summary AlertRules { get; } = new(); public Summary Alerts { get; } = new(); public Summary Notifications { get; } = new(); public bool HasErrors => PdfTemplates.Result().Errors + AlertRules.Result().Errors + Alerts.Result().Errors + Notifications.Result().Errors > 0; public AlertsExcelImportResult ToResult() => new(PdfTemplates.Result(), AlertRules.Result(), Alerts.Result(), Notifications.Result()); }
}
