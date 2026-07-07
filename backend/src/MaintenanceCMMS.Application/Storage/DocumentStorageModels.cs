namespace MaintenanceCMMS.Application.Storage;

public enum DocumentStorageMode
{
    ManualLink = 0,
    LocalSimulation = 1,
    GraphApiReady = 2
}

public enum DocumentStoragePurpose
{
    Document = 0,
    AlertPdf = 1,
    Evidence = 2,
    ImportBackup = 3
}

public enum DocumentStorageStatus
{
    Stored = 0,
    ManualLink = 1,
    PendingManualLink = 2,
    GraphApiReady = 3,
    InvalidPath = 4
}

public sealed record DocumentStorageProviderInfo(
    DocumentStorageMode Mode,
    string Provider,
    bool SupportsUpload,
    bool RequiresManualLink,
    bool GraphConfigured,
    string RootPath,
    string? SiteUrl);

public sealed record DocumentStorageSaveRequest(
    string Module,
    string EntityType,
    string EntityId,
    string FileName,
    string ContentType,
    byte[] Content,
    string UploadedBy,
    DocumentStoragePurpose Purpose = DocumentStoragePurpose.Document,
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    string? OtNumero = null,
    IReadOnlyDictionary<string, string?>? Metadata = null);

public sealed record ManualDocumentLinkRequest(
    string Module,
    string EntityType,
    string EntityId,
    string FileName,
    string Url,
    string LinkedBy,
    DocumentStoragePurpose Purpose = DocumentStoragePurpose.Document,
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    string? OtNumero = null,
    IReadOnlyDictionary<string, string?>? Metadata = null);

public sealed record DocumentStoragePathRequest(
    string Module,
    string EntityType,
    string EntityId,
    DocumentStoragePurpose Purpose = DocumentStoragePurpose.Document,
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    string? OtNumero = null);

public sealed record DocumentStorageFolderRequest(
    string Module,
    string EntityType,
    string EntityId,
    DocumentStoragePurpose Purpose = DocumentStoragePurpose.Document,
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    string? OtNumero = null);

public sealed record DocumentStorageQuery(
    DocumentStoragePurpose? Purpose = null,
    string? Module = null,
    string? EntityType = null,
    string? EntityId = null,
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    string? OtNumero = null,
    bool IncludeInactive = false);

public sealed record DocumentStoragePathValidationResult(
    bool IsValid,
    string RelativePath,
    IReadOnlyCollection<string> Errors);

public sealed record DocumentStorageFolderInfo(
    string RelativePath,
    string? LocalPath,
    string Url,
    DocumentStorageMode Mode,
    bool Created);

public sealed record DocumentStorageInfo(
    string FileKey,
    string FileName,
    string ContentType,
    DocumentStorageMode Mode,
    DocumentStoragePurpose Purpose,
    DocumentStorageStatus Status,
    string Module,
    string EntityType,
    string EntityId,
    string? FaenaCodigo,
    string? ActivoCodigo,
    string? OtNumero,
    string RelativePath,
    string? LocalPath,
    string Url,
    long SizeBytes,
    int Version,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    string? MetadataJson);

public sealed record DocumentStorageDownload(
    string FileName,
    string ContentType,
    byte[] Content);
