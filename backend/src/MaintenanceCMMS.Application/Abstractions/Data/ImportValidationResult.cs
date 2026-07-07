namespace MaintenanceCMMS.Application.Abstractions.Data;

public sealed record ImportValidationResult(
    bool IsValid,
    IReadOnlyCollection<ImportRowValidationError> Errors);

public sealed record ImportRowValidationError(
    int RowNumber,
    string ColumnName,
    string Message);

