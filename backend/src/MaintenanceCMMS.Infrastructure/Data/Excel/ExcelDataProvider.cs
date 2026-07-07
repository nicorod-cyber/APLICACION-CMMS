using ClosedXML.Excel;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Data.Excel;

public sealed class ExcelDataProvider : IDataProvider
{
    private readonly IExcelSchemaRegistry _schemaRegistry;

    private readonly string _basePath;

    public ExcelDataProvider(
        IExcelSchemaRegistry schemaRegistry,
        IOptions<DataProviderSettings> settings)
    {
        _schemaRegistry = schemaRegistry;
        _basePath = ExcelPathResolver.Resolve(settings.Value.ExcelPath);
    }

    public string Name => "Excel";

    public DataProviderType ProviderType => DataProviderType.Excel;

    public string BasePath => _basePath;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_basePath);

        foreach (var schema in _schemaRegistry.GetAll())
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureWorkbook(schema);
        }

        return Task.CompletedTask;
    }

    public async Task<DataProviderHealth> CheckHealthAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        var files = new List<DataProviderFileHealth>();
        var errors = new List<string>();

        foreach (var schema in _schemaRegistry.GetAll())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = GetPath(schema);
            var exists = File.Exists(path);
            var hasDataSheet = false;
            var missingColumns = new List<string>();
            var duplicateKeys = new List<string>();

            if (!exists)
            {
                missingColumns.AddRange(schema.Columns.Select(column => column.Name));
            }
            else
            {
                try
                {
                    using var workbook = OpenWorkbookForRead(schema);
                    hasDataSheet = workbook.TryGetWorksheet(schema.WorksheetName, out var worksheet);
                    if (worksheet is null)
                    {
                        missingColumns.AddRange(schema.Columns.Select(column => column.Name));
                    }
                    else
                    {
                        var headers = ReadHeaders(worksheet);
                        missingColumns.AddRange(schema.Columns
                            .Where(column => column.IsRequired && !headers.ContainsKey(column.Name))
                            .Select(column => column.Name));

                        var rows = ReadWorksheetRows(worksheet, schema, headers);
                        duplicateKeys.AddRange(FindDuplicateNaturalKeys(schema, rows));
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{schema.FileName}: {ex.Message}");
                }
            }

            files.Add(new DataProviderFileHealth(
                schema.SchemaName,
                schema.FileName,
                exists,
                hasDataSheet,
                missingColumns,
                duplicateKeys));
        }

        return new DataProviderHealth(Name, errors.Count == 0 && files.All(file => file.Exists && file.HasDataSheet && file.MissingColumns.Count == 0 && file.DuplicateNaturalKeys.Count == 0), _basePath, files, errors);
    }

    public Task<IReadOnlyList<DataRow>> ReadRowsAsync(string schemaName, CancellationToken cancellationToken)
    {
        var schema = _schemaRegistry.GetRequired(schemaName);
        EnsureWorkbook(schema);

        using var workbook = OpenWorkbookForRead(schema);
        var worksheet = workbook.Worksheet(schema.WorksheetName);
        var headers = ReadHeaders(worksheet);
        ValidateRequiredColumns(schema, headers);

        IReadOnlyList<DataRow> rows = ReadWorksheetRows(worksheet, schema, headers);
        return Task.FromResult(rows);
    }

    public Task SaveRowsAsync(string schemaName, IReadOnlyCollection<DataRow> rows, CancellationToken cancellationToken)
    {
        var schema = _schemaRegistry.GetRequired(schemaName);
        EnsureWorkbook(schema);

        var errors = ExcelRowValidator.Validate(schema, rows);
        if (errors.Count > 0)
        {
            throw new DomainException($"Excel rows are invalid: {string.Join("; ", errors.Select(error => $"row {error.RowNumber} {error.ColumnName}: {error.Message}"))}");
        }

        var duplicateKeys = FindDuplicateNaturalKeys(schema, rows);
        if (duplicateKeys.Count > 0)
        {
            throw new DomainException($"Duplicated natural keys in '{schema.SchemaName}': {string.Join(", ", duplicateKeys)}.");
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(schema.WorksheetName);
        WriteHeaders(worksheet, schema);

        var rowIndex = 2;
        foreach (var row in rows)
        {
            var columnIndex = 1;
            foreach (var column in schema.Columns)
            {
                worksheet.Cell(rowIndex, columnIndex).Value = row.GetValue(column.Name) ?? string.Empty;
                columnIndex++;
            }

            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();
        SaveWorkbook(workbook, schema);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(DataQuery query, CancellationToken cancellationToken)
    {
        var rows = await ReadRowsAsync(query.EntityName, cancellationToken);

        if (typeof(T) == typeof(DataRow))
        {
            return rows.Cast<T>().ToArray();
        }

        return [];
    }

    public Task SaveChangesAsync(UnitOfWorkChanges changes, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private string GetPath(ExcelFileSchema schema) => Path.Combine(_basePath, schema.FileName);

    private void EnsureWorkbook(ExcelFileSchema schema)
    {
        var path = GetPath(schema);
        if (File.Exists(path))
        {
            using var existingWorkbook = OpenWorkbookForRead(schema);
            var currentWorksheet = existingWorkbook.TryGetWorksheet(schema.WorksheetName, out var existingWorksheet)
                ? existingWorksheet
                : existingWorkbook.Worksheets.Add(schema.WorksheetName);

            var headers = ReadHeaders(currentWorksheet);
            var nextColumn = (currentWorksheet.LastColumnUsed()?.ColumnNumber() ?? 0) + 1;
            var changed = false;

            foreach (var column in schema.Columns)
            {
                if (headers.ContainsKey(column.Name))
                {
                    continue;
                }

                var cell = currentWorksheet.Cell(1, nextColumn);
                cell.Value = column.Name;
                cell.Style.Font.Bold = true;
                nextColumn++;
                changed = true;
            }

            if (changed)
            {
                currentWorksheet.Columns().AdjustToContents();
                SaveWorkbook(existingWorkbook, schema);
            }

            return;
        }

        Directory.CreateDirectory(_basePath);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(schema.WorksheetName);
        WriteHeaders(worksheet, schema);
        worksheet.Columns().AdjustToContents();
        SaveWorkbook(workbook, schema);
    }

    private XLWorkbook OpenWorkbookForRead(ExcelFileSchema schema)
    {
        var path = GetPath(schema);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return new XLWorkbook(new MemoryStream(copy.ToArray()));
        }
        catch (IOException ex)
        {
            throw new DomainException($"No se pudo abrir '{schema.FileName}'. Cierra el archivo Excel si esta abierto y vuelve a intentar. Detalle: {ex.Message}");
        }
    }

    private void SaveWorkbook(XLWorkbook workbook, ExcelFileSchema schema)
    {
        try
        {
            workbook.SaveAs(GetPath(schema));
        }
        catch (IOException ex)
        {
            throw new DomainException($"No se pudo guardar '{schema.FileName}'. Cierra el archivo Excel si esta abierto y vuelve a intentar. Detalle: {ex.Message}");
        }
    }

    private static void WriteHeaders(IXLWorksheet worksheet, ExcelFileSchema schema)
    {
        var index = 1;
        foreach (var column in schema.Columns)
        {
            var cell = worksheet.Cell(1, index);
            cell.Value = column.Name;
            cell.Style.Font.Bold = true;
            index++;
        }
    }

    private static Dictionary<string, int> ReadHeaders(IXLWorksheet worksheet)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        for (var column = 1; column <= lastColumn; column++)
        {
            var name = worksheet.Cell(1, column).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(name) && !headers.ContainsKey(name))
            {
                headers[name] = column;
            }
        }

        return headers;
    }

    private static void ValidateRequiredColumns(ExcelFileSchema schema, IReadOnlyDictionary<string, int> headers)
    {
        var missing = schema.Columns
            .Where(column => column.IsRequired && !headers.ContainsKey(column.Name))
            .Select(column => column.Name)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new DomainException($"Excel file '{schema.FileName}' is missing required columns: {string.Join(", ", missing)}.");
        }
    }

    private static IReadOnlyList<DataRow> ReadWorksheetRows(
        IXLWorksheet worksheet,
        ExcelFileSchema schema,
        IReadOnlyDictionary<string, int> headers)
    {
        var result = new List<DataRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowIndex = 2; rowIndex <= lastRow; rowIndex++)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var hasValue = false;

            foreach (var column in schema.Columns)
            {
                if (!headers.TryGetValue(column.Name, out var columnIndex))
                {
                    values[column.Name] = null;
                    continue;
                }

                var value = worksheet.Cell(rowIndex, columnIndex).GetFormattedString().Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasValue = true;
                }

                values[column.Name] = value;
            }

            if (hasValue)
            {
                result.Add(new DataRow(values));
            }
        }

        return result;
    }

    private static IReadOnlyCollection<string> FindDuplicateNaturalKeys(
        ExcelFileSchema schema,
        IReadOnlyCollection<DataRow> rows)
    {
        return rows
            .Select(row => string.Join("|", schema.NaturalKey.Select(key => row.GetValue(key)?.Trim().ToUpperInvariant() ?? string.Empty)))
            .Where(key => !string.IsNullOrWhiteSpace(key.Replace("|", string.Empty)))
            .GroupBy(key => key)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
    }
}
