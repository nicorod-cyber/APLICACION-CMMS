using MaintenanceCMMS.Application.Abstractions.Data;

namespace MaintenanceCMMS.Infrastructure.Data;

public sealed class ImportService : IImportService
{
    private readonly IExcelSchemaRegistry _schemaRegistry;

    private readonly IDataProvider _dataProvider;

    public ImportService(IExcelSchemaRegistry schemaRegistry, IDataProvider dataProvider)
    {
        _schemaRegistry = schemaRegistry;
        _dataProvider = dataProvider;
    }

    public Task<ImportValidationResult> ValidateAsync(
        string schemaName,
        IReadOnlyCollection<DataRow> rows,
        CancellationToken cancellationToken)
    {
        var schema = _schemaRegistry.GetRequired(schemaName);
        var errors = Excel.ExcelRowValidator.Validate(schema, rows);

        return Task.FromResult(new ImportValidationResult(errors.Count == 0, errors));
    }

    public async Task<ImportValidationResult> ImportAsync(
        string schemaName,
        IReadOnlyCollection<DataRow> rows,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(schemaName, rows, cancellationToken);
        if (!validation.IsValid)
        {
            return validation;
        }

        await _dataProvider.SaveRowsAsync(schemaName, rows, cancellationToken);

        return validation;
    }
}

