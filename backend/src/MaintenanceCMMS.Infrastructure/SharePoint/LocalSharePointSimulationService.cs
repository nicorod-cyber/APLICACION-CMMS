using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.SharePoint;

public sealed class LocalSharePointSimulationService : SharePointStorageBase
{
    public LocalSharePointSimulationService(
        CmmsDbContext dbContext,
        IAuditService auditService,
        IOptions<SharePointOptions> options)
        : base(dbContext, auditService, options.Value)
    {
    }

    public override DocumentStorageMode Mode => DocumentStorageMode.LocalSimulation;

    protected override bool SupportsUpload => true;

    public override async Task<DocumentStorageInfo> SaveDocumentAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(request.FileName, nameof(request.FileName));
        if (request.Content.Length == 0)
        {
            throw new DomainException("El documento no contiene bytes para guardar.");
        }

        var relativeFolder = BuildRelativeFolder(new DocumentStoragePathRequest(
            request.Module,
            request.EntityType,
            request.EntityId,
            request.Purpose,
            request.FaenaCodigo,
            request.ActivoCodigo,
            request.OtNumero));
        var errors = ValidateRelativePath(relativeFolder);
        if (errors.Count > 0)
        {
            throw new DomainException($"Ruta SharePoint invalida: {string.Join("; ", errors)}");
        }

        var safeName = SanitizeFileName(request.FileName);
        var fileKey = BuildUniqueFileKey(relativeFolder, safeName);
        var localPath = Path.Combine(ResolveLocalRoot(), fileKey.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(localPath) ?? ResolveLocalRoot();
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(localPath, request.Content, cancellationToken);

        try
        {
            return await SaveMetadataAsync(
                fileKey,
                safeName,
                string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
                Mode,
                request.Purpose,
                DocumentStorageStatus.Stored,
                request.Module,
                request.EntityType,
                request.EntityId,
                request.FaenaCodigo,
                request.ActivoCodigo,
                request.OtNumero,
                relativeFolder,
                localPath,
                BuildVirtualUrl(fileKey),
                request.Content.LongLength,
                request.UploadedBy,
                request.Metadata,
                cancellationToken,
                ComputeChecksum(request.Content));
        }
        catch
        {
            try
            {
                if (File.Exists(localPath)) File.Delete(localPath);
            }
            catch
            {
                // The original database exception remains the operational failure; the orphan is discoverable by storage monitoring.
            }

            throw;
        }
    }
    public override async Task<DocumentStorageInfo> SaveManualLinkAsync(
        ManualDocumentLinkRequest request,
        CancellationToken cancellationToken)
    {
        return await SaveManualMetadataAsync(request, cancellationToken);
    }

    public override Task<DocumentStorageFolderInfo> CreateFolderAsync(
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

        var localPath = Path.Combine(ResolveLocalRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var existed = Directory.Exists(localPath);
        Directory.CreateDirectory(localPath);

        return Task.FromResult(new DocumentStorageFolderInfo(
            relativePath,
            localPath,
            BuildVirtualUrl(relativePath),
            Mode,
            Created: !existed));
    }

    private async Task<DocumentStorageInfo> SaveManualMetadataAsync(
        ManualDocumentLinkRequest request,
        CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(request.Url, nameof(request.Url));
        var relativeFolder = BuildRelativeFolder(new DocumentStoragePathRequest(
            request.Module,
            request.EntityType,
            request.EntityId,
            request.Purpose,
            request.FaenaCodigo,
            request.ActivoCodigo,
            request.OtNumero));
        var safeName = SanitizeFileName(request.FileName);
        var fileKey = BuildUniqueFileKey(relativeFolder, safeName);

        return await SaveMetadataAsync(
            fileKey,
            safeName,
            "text/uri-list",
            Mode,
            request.Purpose,
            DocumentStorageStatus.ManualLink,
            request.Module,
            request.EntityType,
            request.EntityId,
            request.FaenaCodigo,
            request.ActivoCodigo,
            request.OtNumero,
            relativeFolder,
            null,
            request.Url,
            0,
            request.LinkedBy,
            request.Metadata,
            cancellationToken);
    }
}
