using System.Security.Cryptography;
using System.Text.Json;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.SharePoint;

public abstract class SharePointStorageBase : IDocumentStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly CmmsDbContext _dbContext;
    private readonly IAuditService _auditService;

    protected SharePointStorageBase(
        CmmsDbContext dbContext,
        IAuditService auditService,
        SharePointOptions options)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        Options = options;
    }

    public abstract DocumentStorageMode Mode { get; }

    protected SharePointOptions Options { get; }

    protected virtual bool SupportsUpload => false;

    protected virtual bool RequiresManualLink => false;

    public virtual DocumentStorageProviderInfo GetProviderInfo()
    {
        return new DocumentStorageProviderInfo(
            Mode,
            Options.Provider,
            SupportsUpload,
            RequiresManualLink,
            IsGraphConfigured(),
            Mode == DocumentStorageMode.LocalSimulation ? ResolveLocalRoot() : Options.ManualRootUrl,
            string.IsNullOrWhiteSpace(Options.SiteUrl) ? null : Options.SiteUrl);
    }

    public abstract Task<DocumentStorageInfo> SaveDocumentAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken);

    public abstract Task<DocumentStorageInfo> SaveManualLinkAsync(
        ManualDocumentLinkRequest request,
        CancellationToken cancellationToken);

    public Task<DocumentStorageInfo> SaveAlertPdfAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken) =>
        SaveDocumentAsync(request with
        {
            Module = string.IsNullOrWhiteSpace(request.Module) ? "Alerts" : request.Module,
            Purpose = DocumentStoragePurpose.AlertPdf
        }, cancellationToken);

    public Task<DocumentStorageInfo> SaveEvidenceAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken) =>
        SaveDocumentAsync(request with
        {
            Module = string.IsNullOrWhiteSpace(request.Module) ? "Evidence" : request.Module,
            Purpose = DocumentStoragePurpose.Evidence
        }, cancellationToken);

    public Task<DocumentStorageInfo> SaveImportBackupAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken) =>
        SaveDocumentAsync(request with
        {
            Module = string.IsNullOrWhiteSpace(request.Module) ? "Imports" : request.Module,
            Purpose = DocumentStoragePurpose.ImportBackup
        }, cancellationToken);

    public Task<DocumentStoragePathValidationResult> ValidatePathAsync(
        DocumentStoragePathRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var relativePath = BuildRelativeFolder(request);
        var errors = ValidateRelativePath(relativePath);
        return Task.FromResult(new DocumentStoragePathValidationResult(errors.Count == 0, relativePath, errors));
    }

    public virtual Task<DocumentStorageFolderInfo> CreateFolderAsync(
        DocumentStorageFolderRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var relativePath = BuildRelativeFolder(new DocumentStoragePathRequest(
            request.Module,
            request.EntityType,
            request.EntityId,
            request.Purpose,
            request.FaenaCodigo,
            request.ActivoCodigo,
            request.OtNumero));
        var errors = ValidateRelativePath(relativePath);
        if (errors.Count > 0)
        {
            throw new DomainException($"Ruta SharePoint invalida: {string.Join("; ", errors)}");
        }

        return Task.FromResult(new DocumentStorageFolderInfo(
            relativePath,
            null,
            BuildVirtualUrl(relativePath),
            Mode,
            Created: false));
    }

    public async Task<DocumentStorageInfo?> GetAsync(string fileKey, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Files
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.FileKey == fileKey && !item.IsDeleted, cancellationToken);
        return entity is null ? null : ToInfo(entity);
    }

    public async Task<string?> GetLinkAsync(string fileKey, CancellationToken cancellationToken)
    {
        var item = await GetAsync(fileKey, cancellationToken);
        return item?.Url;
    }

    public async Task<IReadOnlyCollection<DocumentStorageInfo>> ListAsync(
        DocumentStorageQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<FileMetadataEntity> files = _dbContext.Files.AsNoTracking();
        if (!query.IncludeInactive)
        {
            files = files.Where(item => !item.IsDeleted && item.Status != DocumentStorageStatus.InvalidPath.ToString());
        }

        if (query.Purpose.HasValue) files = files.Where(item => item.Purpose == query.Purpose.Value.ToString());
        if (!string.IsNullOrWhiteSpace(query.Module)) files = files.Where(item => item.Module == query.Module.Trim());
        if (!string.IsNullOrWhiteSpace(query.EntityType)) files = files.Where(item => item.EntityType == query.EntityType.Trim());
        if (!string.IsNullOrWhiteSpace(query.EntityId)) files = files.Where(item => item.EntityId == query.EntityId.Trim());
        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo)) files = files.Where(item => item.FaenaCode == query.FaenaCodigo.Trim());
        if (!string.IsNullOrWhiteSpace(query.ActivoCodigo)) files = files.Where(item => item.AssetCode == query.ActivoCodigo.Trim());
        if (!string.IsNullOrWhiteSpace(query.OtNumero)) files = files.Where(item => item.WorkOrderNumber == query.OtNumero.Trim());

        return (await files
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToArrayAsync(cancellationToken))
            .Select(ToInfo)
            .ToArray();
    }

    public virtual async Task<DocumentStorageDownload?> DownloadAsync(
        string fileKey,
        CancellationToken cancellationToken)
    {
        var item = await GetAsync(fileKey, cancellationToken);
        if (item is null || string.IsNullOrWhiteSpace(item.LocalPath) || !File.Exists(item.LocalPath))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(item.LocalPath, cancellationToken);
        return new DocumentStorageDownload(item.FileName, item.ContentType, content);
    }

    public async Task<DocumentStorageDeleteResult> DeleteAsync(
        string fileKey,
        string deletedBy,
        bool deletePhysicalContent,
        CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(fileKey, nameof(fileKey));
        DomainGuard.AgainstEmpty(deletedBy, nameof(deletedBy));

        var file = await _dbContext.Files.SingleOrDefaultAsync(item => item.FileKey == fileKey, cancellationToken);
        if (file is null || file.IsDeleted)
        {
            return new DocumentStorageDeleteResult(false, false, false);
        }

        var referenced = await HasReferencesAsync(file.Id, cancellationToken);
        file.IsDeleted = true;
        file.Status = DocumentStorageStatus.Deleted.ToString();
        file.DeletedAtUtc = DateTimeOffset.UtcNow;
        file.DeletedByUserId = deletedBy.Trim();
        file.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var deletedPhysical = false;
        if (deletePhysicalContent && !referenced && !string.IsNullOrWhiteSpace(file.PhysicalLocation) && File.Exists(file.PhysicalLocation))
        {
            File.Delete(file.PhysicalLocation);
            deletedPhysical = true;
        }

        await _auditService.RecordAsync(new AuditEventRequest(
            deletedBy,
            "sharepoint.file_deleted",
            AuditModules.SharePoint,
            "SharePointFile",
            fileKey,
            PreviousValue: file.LogicalUri,
            Severity: AuditSeverity.Medium,
            Detail: referenced ? "Metadata eliminada; el contenido se conserva por referencias activas." : "Metadata eliminada."), cancellationToken);

        return new DocumentStorageDeleteResult(true, deletedPhysical, referenced);
    }

    protected async Task<DocumentStorageInfo> SaveMetadataAsync(
        string fileKey,
        string fileName,
        string contentType,
        DocumentStorageMode mode,
        DocumentStoragePurpose purpose,
        DocumentStorageStatus status,
        string module,
        string entityType,
        string entityId,
        string? faenaCodigo,
        string? activoCodigo,
        string? otNumero,
        string relativePath,
        string? localPath,
        string url,
        long sizeBytes,
        string createdBy,
        IReadOnlyDictionary<string, string?>? metadata,
        CancellationToken cancellationToken,
        string? checksum = null)
    {
        DomainGuard.AgainstEmpty(fileKey, nameof(fileKey));
        DomainGuard.AgainstEmpty(fileName, nameof(fileName));
        DomainGuard.AgainstEmpty(module, nameof(module));
        DomainGuard.AgainstEmpty(entityType, nameof(entityType));
        DomainGuard.AgainstEmpty(entityId, nameof(entityId));
        if (sizeBytes < 0) throw new DomainException("El tamano del archivo no puede ser negativo.");

        var errors = ValidateRelativePath(relativePath);
        if (errors.Count > 0) throw new DomainException($"Ruta SharePoint invalida: {string.Join("; ", errors)}");
        if (await _dbContext.Files.AnyAsync(item => item.FileKey == fileKey, cancellationToken))
        {
            throw new DomainException("La clave logica del archivo ya existe.");
        }

        var previousVersion = await _dbContext.Files
            .Where(item => item.LogicalPath == relativePath && item.FileName == fileName)
            .Select(item => (int?)item.FileVersion)
            .MaxAsync(cancellationToken) ?? 0;
        var entity = new FileMetadataEntity
        {
            FileKey = fileKey.Trim(),
            FileName = fileName.Trim(),
            StoredFileName = fileName.Trim(),
            Extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
            Provider = mode.ToString(),
            StorageMode = mode.ToString(),
            Purpose = purpose.ToString(),
            Module = module.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId.Trim(),
            FaenaCode = EmptyToNull(faenaCodigo),
            AssetCode = EmptyToNull(activoCodigo),
            WorkOrderNumber = EmptyToNull(otNumero),
            LogicalPath = relativePath.Trim(),
            PhysicalLocation = EmptyToNull(localPath),
            LogicalUri = string.IsNullOrWhiteSpace(url) ? BuildVirtualUrl(fileKey) : url.Trim(),
            MimeType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            SizeBytes = sizeBytes,
            Checksum = EmptyToNull(checksum),
            Status = status.ToString(),
            FileVersion = previousVersion + 1,
            MetadataJson = SerializeMetadata(metadata),
            AuthorUserId = createdBy.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.Files.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditService.RecordAsync(new AuditEventRequest(
            createdBy,
            "sharepoint.file_saved",
            AuditModules.SharePoint,
            "SharePointFile",
            fileKey,
            NewValue: entity.LogicalUri,
            Severity: AuditSeverity.Medium,
            Detail: $"{mode} {purpose}: {relativePath}/{fileName}"), cancellationToken);

        return ToInfo(entity);
    }

    protected static string ComputeChecksum(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    protected string ResolveLocalRoot()
    {
        var configuredPath = string.IsNullOrWhiteSpace(Options.LocalPath)
            ? "data/sharepoint-simulated"
            : Options.LocalPath;
        return Path.GetFullPath(configuredPath);
    }

    protected static string BuildRelativeFolder(DocumentStoragePathRequest request)
    {
        var faena = SanitizeSegment(FirstNonEmpty(request.FaenaCodigo, request.EntityType.Equals("Faena", StringComparison.OrdinalIgnoreCase) ? request.EntityId : null), "SinFaena");
        var asset = SanitizeSegment(FirstNonEmpty(request.ActivoCodigo, request.EntityType.Equals("Activo", StringComparison.OrdinalIgnoreCase) ? request.EntityId : null), "SinActivo");
        var ot = SanitizeSegment(FirstNonEmpty(request.OtNumero, request.EntityType.Equals("OT", StringComparison.OrdinalIgnoreCase) ? request.EntityId : null), string.Empty);
        var segments = new List<string> { faena, asset };
        if (!string.IsNullOrWhiteSpace(ot)) segments.Add(ot);
        segments.Add(PurposeFolder(request.Purpose));
        var module = SanitizeSegment(request.Module, string.Empty);
        if (!string.IsNullOrWhiteSpace(module) && !module.Equals("Documents", StringComparison.OrdinalIgnoreCase) && !module.Equals(PurposeFolder(request.Purpose), StringComparison.OrdinalIgnoreCase)) segments.Add(module);
        return string.Join('/', segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
    }

    protected static string BuildUniqueFileKey(string relativeFolder, string fileName)
    {
        var uniqueName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}-{SanitizeFileName(fileName)}";
        return $"{relativeFolder}/{uniqueName}".Replace('\\', '/');
    }

    protected static string SanitizeFileName(string value)
    {
        value = Path.GetFileName(value);
        foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '-');
        return string.IsNullOrWhiteSpace(value) ? "documento.bin" : value.Trim();
    }

    internal static string BuildVirtualUrl(string relativePath) => $"/api/sharepoint/download?fileKey={Uri.EscapeDataString(relativePath)}";

    protected static IReadOnlyCollection<string> ValidateRelativePath(string relativePath)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(relativePath)) errors.Add("La ruta no puede estar vacia.");
        if (Path.IsPathRooted(relativePath)) errors.Add("La ruta debe ser relativa.");
        if (relativePath.Contains("..", StringComparison.Ordinal)) errors.Add("La ruta no puede navegar a directorios superiores.");
        var invalid = Path.GetInvalidPathChars();
        if (relativePath.Any(character => invalid.Contains(character))) errors.Add("La ruta contiene caracteres no validos.");
        return errors;
    }

    private async Task<bool> HasReferencesAsync(Guid fileId, CancellationToken cancellationToken)
    {
        return await _dbContext.DocumentVersions.AnyAsync(item => item.FileId == fileId, cancellationToken) ||
               await _dbContext.WorkOrderEvidences.AnyAsync(item => item.FileId == fileId && item.IsActive, cancellationToken) ||
               await _dbContext.WorkOrderSignatures.AnyAsync(item => item.FileId == fileId && item.IsActive, cancellationToken);
    }

    private static DocumentStorageInfo ToInfo(FileMetadataEntity entity)
    {
        return new DocumentStorageInfo(
            entity.FileKey,
            entity.FileName,
            entity.MimeType ?? "application/octet-stream",
            ParseEnum(entity.StorageMode, ParseEnum(entity.Provider, DocumentStorageMode.LocalSimulation)),
            ParseEnum(entity.Purpose, DocumentStoragePurpose.Document),
            ParseEnum(entity.Status, DocumentStorageStatus.InvalidPath),
            entity.Module,
            entity.EntityType,
            entity.EntityId,
            entity.FaenaCode,
            entity.AssetCode,
            entity.WorkOrderNumber,
            entity.LogicalPath ?? string.Empty,
            entity.PhysicalLocation,
            entity.LogicalUri,
            entity.SizeBytes ?? 0,
            entity.FileVersion,
            entity.CreatedAtUtc,
            entity.AuthorUserId ?? "system",
            entity.MetadataJson);
    }

    private static string? SerializeMetadata(IReadOnlyDictionary<string, string?>? metadata) =>
        metadata is null || metadata.Count == 0 ? null : JsonSerializer.Serialize(metadata, JsonOptions);

    private bool IsGraphConfigured() =>
        !string.IsNullOrWhiteSpace(Options.TenantId) &&
        !string.IsNullOrWhiteSpace(Options.ClientId) &&
        !string.IsNullOrWhiteSpace(Options.SiteId) &&
        !string.IsNullOrWhiteSpace(Options.DriveId);

    private static string PurposeFolder(DocumentStoragePurpose purpose) => purpose switch
    {
        DocumentStoragePurpose.AlertPdf => "AlertasPDF",
        DocumentStoragePurpose.Evidence => "Evidencias",
        DocumentStoragePurpose.ImportBackup => "RespaldosImportacion",
        _ => "Documentos"
    };

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static string SanitizeSegment(string? value, string fallback)
    {
        value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-').ToArray();
        var sanitized = new string(chars).Trim('-', '.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}