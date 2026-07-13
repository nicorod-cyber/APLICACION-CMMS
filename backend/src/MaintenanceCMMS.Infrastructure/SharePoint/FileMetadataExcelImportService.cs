using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.SharePoint;

public sealed class FileMetadataExcelImportService : IFileMetadataExcelImportService
{
    private readonly CmmsDbContext _dbContext;

    public FileMetadataExcelImportService(CmmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FileMetadataExcelImportResult> ImportAsync(
        FileMetadataExcelImportRequest request,
        CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(request.ExcelPath, nameof(request.ExcelPath));
        if (!File.Exists(request.ExcelPath))
        {
            throw new DomainException("No se encontro el archivo Excel de metadata.");
        }

        using var workbook = new XLWorkbook(request.ExcelPath);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new DomainException("El Excel de metadata no contiene hojas.");
        var header = worksheet.FirstRowUsed()
            ?? throw new DomainException("El Excel de metadata no contiene encabezados.");
        var columns = header.CellsUsed()
            .ToDictionary(cell => cell.GetString().Trim(), cell => cell.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

        RequireColumns(columns, "FileKey", "FileName", "ContentType", "Mode", "Purpose", "Status", "Module", "EntityType", "EntityId", "RelativePath", "SizeBytes", "CreatedAtUtc", "CreatedBy");

        var warnings = new List<string>();
        var referencesNotFound = new List<string>();
        var errors = new List<string>();
        var candidates = new List<FileMetadataEntity>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = 0;
        var rowsRead = 0;

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowsRead++;
            var line = row.RowNumber();
            var fileKey = Value(row, columns, "FileKey");
            if (string.IsNullOrWhiteSpace(fileKey))
            {
                errors.Add($"Fila {line}: FileKey es obligatorio.");
                continue;
            }

            if (!keys.Add(fileKey))
            {
                duplicates++;
                warnings.Add($"Fila {line}: FileKey duplicado '{fileKey}' omitido.");
                continue;
            }

            try
            {
                var entity = await CreateEntityAsync(row, columns, line, warnings, referencesNotFound, cancellationToken);
                candidates.Add(entity);
            }
            catch (DomainException ex)
            {
                errors.Add($"Fila {line}: {ex.Message}");
            }
        }

        if (referencesNotFound.Count > 0)
        {
            errors.AddRange(referencesNotFound);
        }

        if (errors.Count > 0)
        {
            return new FileMetadataExcelImportResult(rowsRead, 0, 0, 0, duplicates, errors.Count, warnings.Concat(errors).ToArray(), referencesNotFound);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await _dbContext.Files
            .Where(file => candidates.Select(candidate => candidate.FileKey).Contains(file.FileKey))
            .ToDictionaryAsync(file => file.FileKey, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var inserted = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var candidate in candidates)
        {
            if (!existing.TryGetValue(candidate.FileKey, out var persisted))
            {
                _dbContext.Files.Add(candidate);
                inserted++;
                continue;
            }

            if (Equivalent(persisted, candidate))
            {
                skipped++;
                continue;
            }

            Copy(candidate, persisted);
            updated++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new FileMetadataExcelImportResult(rowsRead, inserted, updated, skipped, duplicates, 0, warnings, referencesNotFound);
    }

    private async Task<FileMetadataEntity> CreateEntityAsync(
        IXLRow row,
        IReadOnlyDictionary<string, int> columns,
        int line,
        ICollection<string> warnings,
        ICollection<string> referencesNotFound,
        CancellationToken cancellationToken)
    {
        var fileKey = Required(Value(row, columns, "FileKey"), "FileKey");
        var fileName = Required(Value(row, columns, "FileName"), "FileName");
        var relativePath = Required(Value(row, columns, "RelativePath"), "RelativePath").Replace('\\', '/');
        var pathErrors = ValidateRelativePath(relativePath);
        if (pathErrors.Count > 0) throw new DomainException(string.Join("; ", pathErrors));

        var mode = ParseEnum<DocumentStorageMode>(Required(Value(row, columns, "Mode"), "Mode"), "Mode");
        var purpose = ParseEnum<DocumentStoragePurpose>(Required(Value(row, columns, "Purpose"), "Purpose"), "Purpose");
        var status = ParseEnum<DocumentStorageStatus>(Required(Value(row, columns, "Status"), "Status"), "Status");
        var sizeBytes = ParseLong(Required(Value(row, columns, "SizeBytes"), "SizeBytes"), "SizeBytes");
        if (sizeBytes < 0) throw new DomainException("SizeBytes no puede ser negativo.");

        var metadataJson = Value(row, columns, "MetadataJson");
        if (!string.IsNullOrWhiteSpace(metadataJson))
        {
            try { JsonDocument.Parse(metadataJson); }
            catch (JsonException) { throw new DomainException("MetadataJson no contiene JSON valido."); }
        }

        var entityType = Required(Value(row, columns, "EntityType"), "EntityType");
        var entityId = Required(Value(row, columns, "EntityId"), "EntityId");
        var faenaCode = EmptyToNull(Value(row, columns, "FaenaCodigo"));
        var assetCode = EmptyToNull(Value(row, columns, "ActivoCodigo"));
        var workOrderNumber = EmptyToNull(Value(row, columns, "OtNumero"));
        await ValidateReferencesAsync(entityType, entityId, faenaCode, assetCode, workOrderNumber, line, referencesNotFound, warnings, cancellationToken);

        var checksum = EmptyToNull(Value(row, columns, "Checksum"));
        if (checksum is null) warnings.Add($"Fila {line}: no existe checksum; se conserva como metadata sin hash.");
        var versionText = Value(row, columns, "Version");
        var fileVersion = string.IsNullOrWhiteSpace(versionText) ? 1 : ParseInt(versionText, "Version");
        if (fileVersion < 1) throw new DomainException("Version debe ser mayor que cero.");

        return new FileMetadataEntity
        {
            FileKey = fileKey,
            FileName = fileName,
            StoredFileName = fileName,
            Extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
            Provider = mode.ToString(),
            StorageMode = mode.ToString(),
            Purpose = purpose.ToString(),
            Module = Required(Value(row, columns, "Module"), "Module"),
            EntityType = entityType,
            EntityId = entityId,
            FaenaCode = faenaCode,
            AssetCode = assetCode,
            WorkOrderNumber = workOrderNumber,
            LogicalPath = relativePath,
            PhysicalLocation = EmptyToNull(Value(row, columns, "LocalPath")),
            LogicalUri = EmptyToNull(Value(row, columns, "Url")) ?? SharePointStorageBase.BuildVirtualUrl(fileKey),
            MimeType = EmptyToNull(Value(row, columns, "ContentType")) ?? "application/octet-stream",
            SizeBytes = sizeBytes,
            Checksum = checksum,
            Status = status.ToString(),
            FileVersion = fileVersion,
            MetadataJson = metadataJson,
            AuthorUserId = Required(Value(row, columns, "CreatedBy"), "CreatedBy"),
            CreatedAtUtc = ParseDateTime(Required(Value(row, columns, "CreatedAtUtc"), "CreatedAtUtc"), "CreatedAtUtc")
        };
    }

    private async Task ValidateReferencesAsync(
        string entityType,
        string entityId,
        string? faenaCode,
        string? assetCode,
        string? workOrderNumber,
        int line,
        ICollection<string> referencesNotFound,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(faenaCode) && !await _dbContext.Faenas.AnyAsync(item => item.Code == faenaCode, cancellationToken))
        {
            referencesNotFound.Add($"Fila {line}: faena '{faenaCode}' no existe.");
        }

        var asset = assetCode ?? (entityType.Equals("Activo", StringComparison.OrdinalIgnoreCase) ? entityId : null);
        if (!string.IsNullOrWhiteSpace(asset) && !await _dbContext.Assets.AnyAsync(item => item.Code == asset, cancellationToken))
        {
            referencesNotFound.Add($"Fila {line}: activo '{asset}' no existe.");
        }

        var workOrder = workOrderNumber ?? (entityType.Equals("OT", StringComparison.OrdinalIgnoreCase) ? entityId : null);
        if (!string.IsNullOrWhiteSpace(workOrder) && !await _dbContext.WorkOrders.AnyAsync(item => item.WorkOrderNumber == workOrder, cancellationToken))
        {
            referencesNotFound.Add($"Fila {line}: OT '{workOrder}' no existe.");
        }

        if (entityType.Equals("Documento", StringComparison.OrdinalIgnoreCase) || entityType.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            if (!await _dbContext.Documents.AnyAsync(item => item.Code == entityId, cancellationToken))
            {
                referencesNotFound.Add($"Fila {line}: documento '{entityId}' no existe.");
            }
        }
        else if (!entityType.Equals("Activo", StringComparison.OrdinalIgnoreCase) &&
                 !entityType.Equals("OT", StringComparison.OrdinalIgnoreCase) &&
                 !entityType.Equals("Documento", StringComparison.OrdinalIgnoreCase) &&
                 !entityType.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Fila {line}: la entidad '{entityType}' se conserva como vinculo operacional no tipado.");
        }
    }

    private static void Copy(FileMetadataEntity source, FileMetadataEntity target)
    {
        target.FileName = source.FileName;
        target.StoredFileName = source.StoredFileName;
        target.Extension = source.Extension;
        target.Provider = source.Provider;
        target.StorageMode = source.StorageMode;
        target.Purpose = source.Purpose;
        target.Module = source.Module;
        target.EntityType = source.EntityType;
        target.EntityId = source.EntityId;
        target.FaenaCode = source.FaenaCode;
        target.AssetCode = source.AssetCode;
        target.WorkOrderNumber = source.WorkOrderNumber;
        target.LogicalPath = source.LogicalPath;
        target.PhysicalLocation = source.PhysicalLocation;
        target.LogicalUri = source.LogicalUri;
        target.MimeType = source.MimeType;
        target.SizeBytes = source.SizeBytes;
        target.Checksum = source.Checksum;
        target.Status = source.Status;
        target.FileVersion = source.FileVersion;
        target.MetadataJson = source.MetadataJson;
        target.AuthorUserId = source.AuthorUserId;
        target.IsDeleted = false;
        target.DeletedAtUtc = null;
        target.DeletedByUserId = null;
        target.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static bool Equivalent(FileMetadataEntity left, FileMetadataEntity right) =>
        left.FileName == right.FileName && left.StoredFileName == right.StoredFileName && left.Extension == right.Extension &&
        left.Provider == right.Provider && left.StorageMode == right.StorageMode && left.Purpose == right.Purpose &&
        left.Module == right.Module && left.EntityType == right.EntityType && left.EntityId == right.EntityId &&
        left.FaenaCode == right.FaenaCode && left.AssetCode == right.AssetCode && left.WorkOrderNumber == right.WorkOrderNumber &&
        left.LogicalPath == right.LogicalPath && left.PhysicalLocation == right.PhysicalLocation && left.LogicalUri == right.LogicalUri &&
        left.MimeType == right.MimeType && left.SizeBytes == right.SizeBytes && left.Checksum == right.Checksum &&
        left.Status == right.Status && left.FileVersion == right.FileVersion && left.MetadataJson == right.MetadataJson &&
        left.AuthorUserId == right.AuthorUserId && !left.IsDeleted;

    private static void RequireColumns(IReadOnlyDictionary<string, int> columns, params string[] required)
    {
        var missing = required.Where(column => !columns.ContainsKey(column)).ToArray();
        if (missing.Length > 0) throw new DomainException($"Faltan columnas requeridas: {string.Join(", ", missing)}.");
    }

    private static string Value(IXLRow row, IReadOnlyDictionary<string, int> columns, string column) =>
        columns.TryGetValue(column, out var index) ? row.Cell(index).GetString().Trim() : string.Empty;

    private static string Required(string value, string column) =>
        string.IsNullOrWhiteSpace(value) ? throw new DomainException($"{column} es obligatorio.") : value.Trim();

    private static TEnum ParseEnum<TEnum>(string value, string column) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : throw new DomainException($"{column} tiene un valor no valido.");

    private static long ParseLong(string value, string column) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : throw new DomainException($"{column} no es numerico.");

    private static int ParseInt(string value, string column) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : throw new DomainException($"{column} no es numerico.");

    private static DateTimeOffset ParseDateTime(string value, string column) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result) ? result : throw new DomainException($"{column} no es una fecha valida.");

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyCollection<string> ValidateRelativePath(string relativePath)
    {
        var errors = new List<string>();
        if (Path.IsPathRooted(relativePath)) errors.Add("RelativePath debe ser relativa.");
        if (relativePath.Contains("..", StringComparison.Ordinal)) errors.Add("RelativePath no puede navegar a directorios superiores.");
        if (relativePath.Any(character => Path.GetInvalidPathChars().Contains(character))) errors.Add("RelativePath contiene caracteres invalidos.");
        return errors;
    }
}
