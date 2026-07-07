namespace MaintenanceCMMS.Application.Imports;

public interface IExcelImportWorkflowService
{
    Task<ExcelImportPreviewResult> UploadAsync(
        ExcelImportUploadCommand command,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ExcelImportListItem>> ListAsync(CancellationToken cancellationToken);

    Task<ExcelImportPreviewResult?> GetPreviewAsync(string id, CancellationToken cancellationToken);

    Task<ExcelImportPreviewResult?> ApproveAsync(string id, string approvedBy, CancellationToken cancellationToken);

    Task<ExcelImportPreviewResult?> RejectAsync(string id, string rejectedBy, string? reason, CancellationToken cancellationToken);

    Task<ExcelImportTemplate> CreateTemplateAsync(string entity, CancellationToken cancellationToken);
}
