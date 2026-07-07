using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Options;

namespace MaintenanceCMMS.Infrastructure.SharePoint;

public abstract class SharePointStorageBase : IDocumentStorageService
{
    protected const string SchemaName = "sharepoint_files";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IDataProvider _dataProvider;
    private readonly IAuditService _auditService;

    protected SharePointStorageBase(
        IDataProvider dataProvider,
        IAuditService auditService,
        SharePointOptions options)
    {
        _dataProvider = dataProvider;
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
        CancellationToken cancellationToken)
    {
        return SaveDocumentAsync(request with
        {
            Module = string.IsNullOrWhiteSpace(request.Module) ? "Alerts" : request.Module,
            Purpose = DocumentStoragePurpose.AlertPdf
        }, cancellationToken);
    }

    public Task<DocumentStorageInfo> SaveEvidenceAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken)
    {
        return SaveDocumentAsync(request with
        {
            Module = string.IsNullOrWhiteSpace(request.Module) ? "Evidence" : request.Module,
            Purpose = DocumentStoragePurpose.Evidence
        }, cancellationToken);
    }

    public Task<DocumentStorageInfo> SaveImportBackupAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken)
    {
        return SaveDocumentAsync(request with
        {
            Module = string.IsNullOrWhiteSpace(request.Module) ? "Imports" : request.Module,
            Purpose = DocumentStoragePurpose.ImportBackup
        }, cancellationToken);
    }

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

    public async Task<DocumentStorageInfo?> GetAsync(
        string fileKey,
        CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(SchemaName, cancellationToken);
        return rows
            .Select(ToInfo)
            .FirstOrDefault(item => Same(item.FileKey, fileKey));
    }

    public async Task<string?> GetLinkAsync(
        string fileKey,
        CancellationToken cancellationToken)
    {
        var item = await GetAsync(fileKey, cancellationToken);
        return item?.Url;
    }

    public async Task<IReadOnlyCollection<DocumentStorageInfo>> ListAsync(
        DocumentStorageQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(SchemaName, cancellationToken);
        return rows
            .Select(ToInfo)
            .Where(item => query.IncludeInactive || item.Status != DocumentStorageStatus.InvalidPath)
            .Where(item => !query.Purpose.HasValue || item.Purpose == query.Purpose)
            .Where(item => string.IsNullOrWhiteSpace(query.Module) || Same(item.Module, query.Module))
            .Where(item => string.IsNullOrWhiteSpace(query.EntityType) || Same(item.EntityType, query.EntityType))
            .Where(item => string.IsNullOrWhiteSpace(query.EntityId) || Same(item.EntityId, query.EntityId))
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.ActivoCodigo) || Same(item.ActivoCodigo, query.ActivoCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.OtNumero) || Same(item.OtNumero, query.OtNumero))
            .OrderByDescending(item => item.CreatedAtUtc)
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
        CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(fileKey, nameof(fileKey));
        DomainGuard.AgainstEmpty(fileName, nameof(fileName));
        DomainGuard.AgainstEmpty(module, nameof(module));
        DomainGuard.AgainstEmpty(entityType, nameof(entityType));
        DomainGuard.AgainstEmpty(entityId, nameof(entityId));

        var rows = (await _dataProvider.ReadRowsAsync(SchemaName, cancellationToken)).ToList();
        var previousVersions = rows
            .Select(ToInfo)
            .Where(item => Same(item.RelativePath, relativePath) && Same(item.FileName, fileName))
            .ToArray();

        rows.RemoveAll(row => Same(row.GetValue("FileKey"), fileKey));
        var version = previousVersions.Length == 0 ? 1 : previousVersions.Max(item => item.Version) + 1;
        var createdAtUtc = DateTimeOffset.UtcNow;
        var metadataJson = metadata is null || metadata.Count == 0
            ? null
            : JsonSerializer.Serialize(metadata, JsonOptions);

        var row = new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["FileKey"] = fileKey,
            ["FileName"] = fileName,
            ["ContentType"] = contentType,
            ["Mode"] = mode.ToString(),
            ["Purpose"] = purpose.ToString(),
            ["Status"] = status.ToString(),
            ["Module"] = module,
            ["EntityType"] = entityType,
            ["EntityId"] = entityId,
            ["FaenaCodigo"] = EmptyToNull(faenaCodigo),
            ["ActivoCodigo"] = EmptyToNull(activoCodigo),
            ["OtNumero"] = EmptyToNull(otNumero),
            ["RelativePath"] = relativePath,
            ["LocalPath"] = EmptyToNull(localPath),
            ["Url"] = EmptyToNull(url),
            ["SizeBytes"] = sizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Version"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["CreatedAtUtc"] = createdAtUtc.ToString("O"),
            ["CreatedBy"] = createdBy,
            ["MetadataJson"] = metadataJson
        });

        rows.Add(row);
        await _dataProvider.SaveRowsAsync(SchemaName, rows, cancellationToken);

        await _auditService.RecordAsync(new AuditEventRequest(
            createdBy,
            "sharepoint.file_saved",
            AuditModules.SharePoint,
            "SharePointFile",
            fileKey,
            NewValue: url,
            Severity: AuditSeverity.Medium,
            Detail: $"{mode} {purpose}: {relativePath}/{fileName}"), cancellationToken);

        return ToInfo(row);
    }

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
        if (!string.IsNullOrWhiteSpace(ot))
        {
            segments.Add(ot);
        }

        segments.Add(PurposeFolder(request.Purpose));

        var module = SanitizeSegment(request.Module, string.Empty);
        if (!string.IsNullOrWhiteSpace(module) &&
            !module.Equals("Documents", StringComparison.OrdinalIgnoreCase) &&
            !module.Equals(PurposeFolder(request.Purpose), StringComparison.OrdinalIgnoreCase))
        {
            segments.Add(module);
        }

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
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return string.IsNullOrWhiteSpace(value) ? "documento.bin" : value.Trim();
    }

    protected static string BuildVirtualUrl(string relativePath)
    {
        return $"/api/sharepoint/download?fileKey={Uri.EscapeDataString(relativePath)}";
    }

    protected static IReadOnlyCollection<string> ValidateRelativePath(string relativePath)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            errors.Add("La ruta no puede estar vacia.");
        }

        if (Path.IsPathRooted(relativePath))
        {
            errors.Add("La ruta debe ser relativa.");
        }

        if (relativePath.Contains("..", StringComparison.Ordinal))
        {
            errors.Add("La ruta no puede navegar a directorios superiores.");
        }

        var invalid = Path.GetInvalidPathChars();
        if (relativePath.Any(character => invalid.Contains(character)))
        {
            errors.Add("La ruta contiene caracteres no validos.");
        }

        return errors;
    }

    private static DocumentStorageInfo ToInfo(DataRow row)
    {
        return new DocumentStorageInfo(
            row.GetValue("FileKey")?.Trim() ?? string.Empty,
            row.GetValue("FileName")?.Trim() ?? string.Empty,
            row.GetValue("ContentType")?.Trim() ?? "application/octet-stream",
            ParseEnum(row.GetValue("Mode"), DocumentStorageMode.LocalSimulation),
            ParseEnum(row.GetValue("Purpose"), DocumentStoragePurpose.Document),
            ParseEnum(row.GetValue("Status"), DocumentStorageStatus.Stored),
            row.GetValue("Module")?.Trim() ?? string.Empty,
            row.GetValue("EntityType")?.Trim() ?? string.Empty,
            row.GetValue("EntityId")?.Trim() ?? string.Empty,
            EmptyToNull(row.GetValue("FaenaCodigo")),
            EmptyToNull(row.GetValue("ActivoCodigo")),
            EmptyToNull(row.GetValue("OtNumero")),
            row.GetValue("RelativePath")?.Trim() ?? string.Empty,
            EmptyToNull(row.GetValue("LocalPath")),
            row.GetValue("Url")?.Trim() ?? string.Empty,
            long.TryParse(row.GetValue("SizeBytes"), out var sizeBytes) ? sizeBytes : 0,
            int.TryParse(row.GetValue("Version"), out var version) ? version : 1,
            DateTimeOffset.TryParse(row.GetValue("CreatedAtUtc"), out var createdAtUtc) ? createdAtUtc : DateTimeOffset.MinValue,
            row.GetValue("CreatedBy")?.Trim() ?? "system",
            EmptyToNull(row.GetValue("MetadataJson")));
    }

    private bool IsGraphConfigured()
    {
        return !string.IsNullOrWhiteSpace(Options.TenantId) &&
               !string.IsNullOrWhiteSpace(Options.ClientId) &&
               !string.IsNullOrWhiteSpace(Options.SiteId) &&
               !string.IsNullOrWhiteSpace(Options.DriveId);
    }

    private static string PurposeFolder(DocumentStoragePurpose purpose)
    {
        return purpose switch
        {
            DocumentStoragePurpose.AlertPdf => "AlertasPDF",
            DocumentStoragePurpose.Evidence => "Evidencias",
            DocumentStoragePurpose.ImportBackup => "RespaldosImportacion",
            _ => "Documentos"
        };
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static string SanitizeSegment(string? value, string fallback)
    {
        value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-')
            .ToArray();

        var sanitized = new string(chars).Trim('-', '.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool Same(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
