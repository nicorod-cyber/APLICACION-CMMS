using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Imports;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Imports;

/// <summary>Excel is parsed only at the boundary; workflow state is relational.</summary>
public sealed class ExcelImportWorkflowService : IExcelImportWorkflowService
{
    private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CmmsDbContext _db;
    private readonly IExcelSchemaRegistry _schemas;
    private readonly PostgreSqlImportHandlerResolver _handlers;
    private readonly IAuditService _audit;
    private readonly IDocumentStorageService _storage;

    public ExcelImportWorkflowService(CmmsDbContext db, IExcelSchemaRegistry schemas, PostgreSqlImportHandlerResolver handlers, IAuditService audit, IDocumentStorageService storage)
    { _db = db; _schemas = schemas; _handlers = handlers; _audit = audit; _storage = storage; }

    public async Task<ExcelImportPreviewResult> UploadAsync(ExcelImportUploadCommand command, CancellationToken ct)
    {
        DomainGuard.AgainstEmpty(command.Entity, nameof(command.Entity)); DomainGuard.AgainstEmpty(command.OriginalFileName, nameof(command.OriginalFileName)); DomainGuard.AgainstEmpty(command.UploadedBy, nameof(command.UploadedBy));
        var schema = Schema(command.Entity); var handler = _handlers.GetRequired(schema.SchemaName); var workbook = ReadWorkbook(command.Content, schema);
        var input = workbook.Rows.Select((values, index) => new PostgreSqlImportRow(index + 2, values)).ToArray();
        var preview = await PreviewAsync(schema, handler, workbook.Headers, input, ct);
        var id = Guid.NewGuid(); var fileName = Path.GetFileName(command.OriginalFileName);
        var stored = await _storage.SaveImportBackupAsync(new DocumentStorageSaveRequest("Imports", "ExcelImport", id.ToString("D"), fileName, ContentType, command.Content, command.UploadedBy, DocumentStoragePurpose.ImportBackup, Metadata: new Dictionary<string, string?> { ["SchemaName"] = schema.SchemaName, ["SimulateOnly"] = command.SimulateOnly.ToString() }), ct);
        var fileId = await _db.Files.Where(file => file.FileKey == stored.FileKey).Select(file => (Guid?)file.Id).SingleAsync(ct);
        var status = preview.Errors.Count == 0 && !command.SimulateOnly ? ImportStatus.PendingApproval : ImportStatus.Validating;
        var entity = new ImportEntity { Id = id, EntityName = command.Entity.Trim(), SchemaName = schema.SchemaName, OriginalFileName = fileName, FileId = fileId, SimulateOnly = command.SimulateOnly, Status = (int)status, UploadedByUserId = command.UploadedBy.Trim(), UploadedAtUtc = DateTimeOffset.UtcNow,
            Rows = preview.Rows.Select(row => new ImportRowEntity { RowNumber = row.RowNumber, Operation = row.Operation, InputSnapshot = JsonSerializer.Serialize(row.Values, JsonOptions) }).ToList(),
            Errors = preview.Errors.Select(error => new ImportErrorEntity { RowNumber = error.RowNumber, ColumnName = Null(error.ColumnName), Message = error.Message }).ToList(),
            Events = [new ImportEventEntity { Status = (int)status, UserId = command.UploadedBy.Trim(), OccurredAtUtc = DateTimeOffset.UtcNow, Detail = $"Archivo recibido para {schema.SchemaName}. Simulación: {command.SimulateOnly}." }] };
        _db.Imports.Add(entity); await _db.SaveChangesAsync(ct); await AuditAsync(command.UploadedBy, "import.uploaded", entity, preview.Errors.Count == 0 ? AuditSeverity.Medium : AuditSeverity.High, null, ct);
        return ToPreview(entity);
    }

    public async Task<IReadOnlyCollection<ExcelImportListItem>> ListAsync(CancellationToken ct) =>
        (await _db.Imports.AsNoTracking().Include(item => item.Rows).Include(item => item.Errors).OrderByDescending(item => item.UploadedAtUtc).ToArrayAsync(ct)).Select(ToList).ToArray();

    public async Task<ExcelImportPreviewResult?> GetPreviewAsync(string id, CancellationToken ct)
    { if (!Guid.TryParse(id, out var key)) return null; var entity = await LoadAsync(key, false, ct); return entity is null ? null : ToPreview(entity); }

    public async Task<ExcelImportPreviewResult?> ApproveAsync(string id, string approvedBy, CancellationToken ct)
    {
        DomainGuard.AgainstEmpty(approvedBy, nameof(approvedBy)); if (!Guid.TryParse(id, out var key)) return null;
        var entity = await LoadAsync(key, true, ct); if (entity is null) return null;
        await _db.Entry(entity).ReloadAsync(ct);
        if (entity.SimulateOnly) throw new DomainException("Las simulaciones no se pueden aprobar ni aplicar al maestro oficial.");
        if ((ImportStatus)entity.Status is ImportStatus.Applied or ImportStatus.Rejected) throw new DomainException("La importación ya fue cerrada.");
        var rows = entity.Rows.OrderBy(row => row.RowNumber).Select(Input).ToArray(); var analysis = await _handlers.GetRequired(entity.SchemaName).AnalyzeAsync(rows, ct); ReplacePreview(entity, analysis);
        if (entity.Errors.Count > 0) { entity.Status = (int)ImportStatus.Validating; _db.ImportEvents.Add(Event(entity.Id, entity.Status, approvedBy, "Aprobación bloqueada por errores de validación actuales.")); await _db.SaveChangesAsync(ct); throw new DomainException("No se puede aprobar una importación con errores. Revise el preview actualizado."); }
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await _handlers.GetRequired(entity.SchemaName).ApplyAsync(rows, approvedBy.Trim(), ct);
            entity.Status = (int)ImportStatus.Applied; entity.AppliedAtUtc = DateTimeOffset.UtcNow; entity.AppliedByUserId = approvedBy.Trim(); entity.RejectReason = null; _db.ImportEvents.Add(Event(entity.Id, entity.Status, approvedBy, "Importación aplicada al maestro relacional."));
            await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            _db.ChangeTracker.Clear(); var failed = await LoadAsync(key, true, ct) ?? throw new InvalidOperationException("No se encontró la importación tras revertir la transacción."); failed.Status = (int)ImportStatus.Failed; failed.RejectReason = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message; _db.ImportEvents.Add(Event(failed.Id, failed.Status, approvedBy, "La aplicación falló; no se confirmaron cambios del maestro.")); await _db.SaveChangesAsync(ct); await AuditAsync(approvedBy, "import.failed", failed, AuditSeverity.Critical, ex.Message, ct); throw;
        }
        await AuditAsync(approvedBy, "import.applied", entity, AuditSeverity.Critical, "Aprobación de importación Excel", ct); return ToPreview(entity);
    }

    public async Task<ExcelImportPreviewResult?> RejectAsync(string id, string rejectedBy, string? reason, CancellationToken ct)
    {
        DomainGuard.AgainstEmpty(rejectedBy, nameof(rejectedBy)); if (!Guid.TryParse(id, out var key)) return null; _db.ChangeTracker.Clear(); var entity = await LoadAsync(key, true, ct); if (entity is null) return null;
        await _db.Entry(entity).ReloadAsync(ct);
        if ((ImportStatus)entity.Status == ImportStatus.Applied) throw new DomainException("No se puede rechazar una importación ya aplicada.");
        entity.Status = (int)ImportStatus.Rejected; entity.RejectedAtUtc = DateTimeOffset.UtcNow; entity.RejectedByUserId = rejectedBy.Trim(); entity.RejectReason = Null(reason); _db.ImportEvents.Add(Event(entity.Id, entity.Status, rejectedBy, "Importación rechazada.")); await _db.SaveChangesAsync(ct); await AuditAsync(rejectedBy, "import.rejected", entity, AuditSeverity.High, reason, ct); return ToPreview(entity);
    }

    public Task<ExcelImportTemplate> CreateTemplateAsync(string entity, CancellationToken ct)
    {
        var schema = Schema(entity); _handlers.GetRequired(schema.SchemaName); using var workbook = new XLWorkbook(); var sheet = workbook.Worksheets.Add(schema.WorksheetName); var index = 1;
        foreach (var column in schema.Columns) { var cell = sheet.Cell(1, index++); cell.Value = column.Name; cell.Style.Font.Bold = true; cell.Style.Fill.BackgroundColor = column.IsRequired ? XLColor.LightYellow : XLColor.White; }
        sheet.Columns().AdjustToContents(); using var stream = new MemoryStream(); workbook.SaveAs(stream); return Task.FromResult(new ExcelImportTemplate($"plantilla_{schema.SchemaName}.xlsx", ContentType, stream.ToArray()));
    }

    private async Task<PreviewData> PreviewAsync(ExcelFileSchema schema, IPostgreSqlImportHandler handler, IReadOnlyCollection<string> headers, IReadOnlyCollection<PostgreSqlImportRow> rows, CancellationToken ct)
    {
        var errors = Validate(schema, headers, rows).ToList(); var analysis = await handler.AnalyzeAsync(rows, ct); errors.AddRange(analysis.SelectMany(item => item.Errors)); var results = analysis.ToDictionary(item => item.RowNumber); var byRow = errors.GroupBy(error => error.RowNumber).ToDictionary(group => group.Key, group => (IReadOnlyCollection<ExcelImportValidationError>)group.ToArray());
        var output = rows.Select(row => { var rowErrors = byRow.GetValueOrDefault(row.RowNumber, Array.Empty<ExcelImportValidationError>()); return new ExcelImportPreviewRow(row.RowNumber, row.Values, rowErrors.Count > 0 ? "Error" : results[row.RowNumber].Operation, rowErrors); }).ToArray();
        return new PreviewData(new ImportPreviewSummary(output.Length, output.Count(row => row.Operation == "Nuevo"), output.Count(row => row.Operation == "Actualizado"), output.Count(row => row.Operation == "SinCambios"), output.Count(row => row.Errors.Count > 0), output.Count(row => row.Errors.Any(error => error.Message.Contains("repite", StringComparison.OrdinalIgnoreCase)))), output, errors);
    }

    private static IReadOnlyCollection<ExcelImportValidationError> Validate(ExcelFileSchema schema, IReadOnlyCollection<string> headers, IReadOnlyCollection<PostgreSqlImportRow> rows)
    {
        var errors = schema.Columns.Where(column => column.IsRequired && !headers.Contains(column.Name, StringComparer.OrdinalIgnoreCase)).Select(column => new ExcelImportValidationError(1, column.Name, $"La columna requerida '{column.Name}' no existe.")).ToList();
        foreach (var row in rows) foreach (var column in schema.Columns)
        {
            var value = row.Values.TryGetValue(column.Name, out var found) ? found?.Trim() : null;
            if (column.IsRequired && string.IsNullOrWhiteSpace(value)) { errors.Add(new ExcelImportValidationError(row.RowNumber, column.Name, "El valor es obligatorio.")); continue; } if (string.IsNullOrWhiteSpace(value)) continue;
            var valid = column.Type switch { ExcelColumnType.Number => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _) || decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("es-CL"), out _), ExcelColumnType.Date => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _) || DateOnly.TryParse(value, CultureInfo.GetCultureInfo("es-CL"), DateTimeStyles.None, out _), ExcelColumnType.Boolean => value is "1" or "0" || bool.TryParse(value, out _) || value.Equals("si", StringComparison.OrdinalIgnoreCase) || value.Equals("sí", StringComparison.OrdinalIgnoreCase) || value.Equals("activo", StringComparison.OrdinalIgnoreCase) || value.Equals("inactivo", StringComparison.OrdinalIgnoreCase), _ => true };
            if (!valid) errors.Add(new ExcelImportValidationError(row.RowNumber, column.Name, $"El valor '{value}' no tiene el formato {column.Type}."));
        }
        return errors;
    }

    private ExcelFileSchema Schema(string entity) => _schemas.GetRequired(entity.Trim().ToLowerInvariant() switch { "activos" or "assets" => "activos", "ubicaciones" => "ubicaciones_tecnicas", "almacenes" => "bodegas", _ => entity.Trim().ToLowerInvariant() });
    private static ParsedWorkbook ReadWorkbook(byte[] content, ExcelFileSchema schema)
    {
        using var stream = new MemoryStream(content); using var workbook = new XLWorkbook(stream); var sheet = workbook.Worksheets.FirstOrDefault(item => item.Name.Equals(schema.WorksheetName, StringComparison.OrdinalIgnoreCase)) ?? workbook.Worksheets.First(); var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= (sheet.LastColumnUsed()?.ColumnNumber() ?? 0); column++) { var name = sheet.Cell(1, column).GetString().Trim(); if (name.Length > 0 && !headers.ContainsKey(name)) headers[name] = column; }
        var rows = new List<IReadOnlyDictionary<string, string?>>(); for (var number = 2; number <= (sheet.LastRowUsed()?.RowNumber() ?? 1); number++) { var values = headers.ToDictionary(header => header.Key, header => (string?)sheet.Cell(number, header.Value).GetFormattedString().Trim(), StringComparer.OrdinalIgnoreCase); foreach (var column in schema.Columns) values.TryAdd(column.Name, null); if (values.Values.Any(value => !string.IsNullOrWhiteSpace(value))) rows.Add(values); }
        return new ParsedWorkbook(headers.Keys.ToArray(), rows);
    }

    private async Task<ImportEntity?> LoadAsync(Guid id, bool tracking, CancellationToken ct) { IQueryable<ImportEntity> query = _db.Imports.Include(item => item.Rows).Include(item => item.Errors).Include(item => item.Events); if (!tracking) query = query.AsNoTracking(); return await query.SingleOrDefaultAsync(item => item.Id == id, ct); }
    private void ReplacePreview(ImportEntity entity, IReadOnlyCollection<PostgreSqlImportRowResult> results) { _db.ImportErrors.RemoveRange(entity.Errors); entity.Errors.Clear(); var byRow = results.ToDictionary(item => item.RowNumber); foreach (var row in entity.Rows) row.Operation = byRow[row.RowNumber].Operation; foreach (var error in results.SelectMany(item => item.Errors)) entity.Errors.Add(new ImportErrorEntity { RowNumber = error.RowNumber, ColumnName = Null(error.ColumnName), Message = error.Message }); }
    private static PostgreSqlImportRow Input(ImportRowEntity row) => new(row.RowNumber, JsonSerializer.Deserialize<Dictionary<string, string?>>(row.InputSnapshot, JsonOptions) ?? new Dictionary<string, string?>());
    private async Task AuditAsync(string user, string action, ImportEntity entity, AuditSeverity severity, string? reason, CancellationToken ct) => await _audit.RecordAsync(new AuditEventRequest(user, action, AuditModules.Imports, "ExcelImport", entity.Id.ToString("D"), NewValue: entity.SchemaName, Severity: severity, Reason: reason, Detail: $"Importación {entity.OriginalFileName}: {(ImportStatus)entity.Status}."), ct);
    private static ImportEventEntity Event(Guid importId, int status, string user, string detail) => new() { ImportId = importId, Status = status, UserId = user.Trim(), OccurredAtUtc = DateTimeOffset.UtcNow, Detail = detail };
    private static ExcelImportPreviewResult ToPreview(ImportEntity entity) => new(ToList(entity), entity.Rows.OrderBy(row => row.RowNumber).Select(row => new ExcelImportPreviewRow(row.RowNumber, Input(row).Values, row.Operation, entity.Errors.Where(error => error.RowNumber == row.RowNumber).Select(Error).ToArray())).ToArray(), entity.Errors.Select(Error).ToArray());
    private static ExcelImportListItem ToList(ImportEntity entity) => new(entity.Id.ToString("D"), entity.EntityName, entity.SchemaName, entity.OriginalFileName, (ImportStatus)entity.Status, entity.SimulateOnly, entity.UploadedAtUtc, entity.UploadedByUserId, entity.AppliedAtUtc, entity.AppliedByUserId, entity.RejectedAtUtc, entity.RejectedByUserId, entity.RejectReason, new ImportPreviewSummary(entity.Rows.Count, entity.Rows.Count(row => row.Operation == "Nuevo"), entity.Rows.Count(row => row.Operation == "Actualizado"), entity.Rows.Count(row => row.Operation == "SinCambios"), entity.Rows.Count(row => row.Operation == "Error"), entity.Errors.Count(error => error.Message.Contains("repite", StringComparison.OrdinalIgnoreCase))));
    private static ExcelImportValidationError Error(ImportErrorEntity error) => new(error.RowNumber, error.ColumnName ?? string.Empty, error.Message);
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private sealed record ParsedWorkbook(IReadOnlyCollection<string> Headers, IReadOnlyCollection<IReadOnlyDictionary<string, string?>> Rows);
    private sealed record PreviewData(ImportPreviewSummary Summary, IReadOnlyCollection<ExcelImportPreviewRow> Rows, IReadOnlyCollection<ExcelImportValidationError> Errors);
}