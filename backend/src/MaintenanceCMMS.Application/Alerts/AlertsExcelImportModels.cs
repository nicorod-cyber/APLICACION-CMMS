namespace MaintenanceCMMS.Application.Alerts;

public sealed record AlertsExcelImportRequest(
    string PdfTemplatesPath,
    string AlertRulesPath,
    string AlertsPath,
    string NotificationsPath);

public sealed record AlertsExcelImportSummary(
    int RowsRead,
    int Inserted,
    int Updated,
    int Skipped,
    int Duplicates,
    int Errors,
    IReadOnlyCollection<string> Warnings,
    IReadOnlyCollection<string> ReferencesNotFound);

public sealed record AlertsExcelImportResult(
    AlertsExcelImportSummary PdfTemplates,
    AlertsExcelImportSummary AlertRules,
    AlertsExcelImportSummary Alerts,
    AlertsExcelImportSummary Notifications);

public interface IAlertsExcelImportService
{
    Task<AlertsExcelImportResult> ImportAsync(AlertsExcelImportRequest request, CancellationToken cancellationToken);
}
