using MaintenanceCMMS.Application.Abstractions.Data;

namespace MaintenanceCMMS.Infrastructure.Data.Excel;

public static class ExcelRowValidator
{
    public static IReadOnlyCollection<ImportRowValidationError> Validate(
        ExcelFileSchema schema,
        IReadOnlyCollection<DataRow> rows)
    {
        var errors = new List<ImportRowValidationError>();
        var rowNumber = 2;

        foreach (var row in rows)
        {
            foreach (var column in schema.Columns)
            {
                var value = row.GetValue(column.Name);

                if (column.IsRequired && string.IsNullOrWhiteSpace(value))
                {
                    errors.Add(new ImportRowValidationError(rowNumber, column.Name, "Required value is missing."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!IsValidType(value, column.Type))
                {
                    errors.Add(new ImportRowValidationError(rowNumber, column.Name, $"Expected {column.Type}."));
                }
            }

            rowNumber++;
        }

        errors.AddRange(FindDuplicateNaturalKeys(schema, rows));

        return errors;
    }

    private static bool IsValidType(string value, ExcelColumnType type)
    {
        return type switch
        {
            ExcelColumnType.Text => true,
            ExcelColumnType.Number => decimal.TryParse(value, out _),
            ExcelColumnType.Date => DateOnly.TryParse(value, out _) || DateTimeOffset.TryParse(value, out _),
            ExcelColumnType.Boolean => bool.TryParse(value, out _) || value is "0" or "1" or "SI" or "NO" or "Si" or "No",
            _ => false
        };
    }

    private static IReadOnlyCollection<ImportRowValidationError> FindDuplicateNaturalKeys(
        ExcelFileSchema schema,
        IReadOnlyCollection<DataRow> rows)
    {
        return rows
            .Select((row, index) => new
            {
                RowNumber = index + 2,
                Key = string.Join("|", schema.NaturalKey.Select(key => row.GetValue(key)?.Trim().ToUpperInvariant() ?? string.Empty))
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key.Replace("|", string.Empty)))
            .GroupBy(item => item.Key)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => new ImportRowValidationError(item.RowNumber, string.Join("+", schema.NaturalKey), $"Duplicated natural key '{group.Key}'.")))
            .ToArray();
    }
}

