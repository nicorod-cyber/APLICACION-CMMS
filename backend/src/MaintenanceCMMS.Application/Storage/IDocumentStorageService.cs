namespace MaintenanceCMMS.Application.Storage;

public interface IDocumentStorageService
{
    DocumentStorageMode Mode { get; }

    DocumentStorageProviderInfo GetProviderInfo();

    Task<DocumentStorageInfo> SaveDocumentAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken);

    Task<DocumentStorageInfo> SaveManualLinkAsync(
        ManualDocumentLinkRequest request,
        CancellationToken cancellationToken);

    Task<DocumentStorageInfo> SaveAlertPdfAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken);

    Task<DocumentStorageInfo> SaveEvidenceAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken);

    Task<DocumentStorageInfo> SaveImportBackupAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken);

    Task<DocumentStoragePathValidationResult> ValidatePathAsync(
        DocumentStoragePathRequest request,
        CancellationToken cancellationToken);

    Task<DocumentStorageFolderInfo> CreateFolderAsync(
        DocumentStorageFolderRequest request,
        CancellationToken cancellationToken);

    Task<DocumentStorageInfo?> GetAsync(
        string fileKey,
        CancellationToken cancellationToken);

    Task<string?> GetLinkAsync(
        string fileKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DocumentStorageInfo>> ListAsync(
        DocumentStorageQuery query,
        CancellationToken cancellationToken);

    Task<DocumentStorageDownload?> DownloadAsync(
        string fileKey,
        CancellationToken cancellationToken);

    Task<DocumentStorageDeleteResult> DeleteAsync(
        string fileKey,
        string deletedBy,
        bool deletePhysicalContent,
        CancellationToken cancellationToken);
}

public interface IFileMetadataExcelImportService
{
    Task<FileMetadataExcelImportResult> ImportAsync(
        FileMetadataExcelImportRequest request,
        CancellationToken cancellationToken);
}
