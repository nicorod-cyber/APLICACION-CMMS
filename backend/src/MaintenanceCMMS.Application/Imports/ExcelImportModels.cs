using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Application.Imports;

public sealed record ExcelImportUploadCommand(
    string Entity,
    string OriginalFileName,
    byte[] Content,
    string UploadedBy,
    bool SimulateOnly);

public sealed record ExcelImportListItem(
    string Id,
    string Entity,
    string SchemaName,
    string OriginalFileName,
    ImportStatus Status,
    bool SimulateOnly,
    DateTimeOffset UploadedAtUtc,
    string UploadedBy,
    DateTimeOffset? AppliedAtUtc,
    string? AppliedBy,
    DateTimeOffset? RejectedAtUtc,
    string? RejectedBy,
    string? RejectReason,
    ImportPreviewSummary Summary);

public sealed record ExcelImportPreview(
    ExcelImportListItem Import,
    IReadOnlyCollection<ExcelImportPreviewRow> Rows,
    IReadOnlyCollection<ExcelImportValidationError> Errors);

public sealed record ExcelImportPreviewRow(
    int RowNumber,
    IReadOnlyDictionary<string, string?> Values,
    string Operation,
    IReadOnlyCollection<ExcelImportValidationError> Errors);

public sealed record ExcelImportPreviewResult(
    ExcelImportListItem Import,
    IReadOnlyCollection<ExcelImportPreviewRow> Rows,
    IReadOnlyCollection<ExcelImportValidationError> Errors);

public sealed record ExcelImportValidationError(
    int RowNumber,
    string ColumnName,
    string Message);

public sealed record ImportPreviewSummary(
    int TotalRows,
    int NewRows,
    int UpdatedRows,
    int UnchangedRows,
    int ErrorRows,
    int DuplicateRows);

public sealed record ExcelImportTemplate(
    string FileName,
    string ContentType,
    byte[] Content);
