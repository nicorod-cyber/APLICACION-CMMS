namespace MaintenanceCMMS.Application.Abstractions.Data;

public enum ExcelColumnType
{
    Text = 0,
    Number = 1,
    Date = 2,
    Boolean = 3
}

public sealed record ExcelColumnSchema(
    string Name,
    ExcelColumnType Type,
    bool IsRequired);

public sealed record ExcelFileSchema(
    string SchemaName,
    string FileName,
    string WorksheetName,
    IReadOnlyCollection<ExcelColumnSchema> Columns,
    IReadOnlyCollection<string> NaturalKey,
    bool AllowsUpdate,
    bool RequiresApproval);

public interface IExcelSchemaRegistry
{
    IReadOnlyCollection<ExcelFileSchema> GetAll();

    ExcelFileSchema GetRequired(string schemaName);
}

