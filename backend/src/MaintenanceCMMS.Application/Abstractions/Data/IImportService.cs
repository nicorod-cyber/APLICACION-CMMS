namespace MaintenanceCMMS.Application.Abstractions.Data;

public interface IImportService
{
    Task<ImportValidationResult> ValidateAsync(
        string schemaName,
        IReadOnlyCollection<DataRow> rows,
        CancellationToken cancellationToken);

    Task<ImportValidationResult> ImportAsync(
        string schemaName,
        IReadOnlyCollection<DataRow> rows,
        CancellationToken cancellationToken);
}

