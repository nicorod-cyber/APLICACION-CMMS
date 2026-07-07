using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.SharePoint;

public sealed class GraphSharePointService : SharePointStorageBase
{
    public GraphSharePointService(
        IDataProvider dataProvider,
        IAuditService auditService,
        IOptions<SharePointOptions> options)
        : base(dataProvider, auditService, options.Value)
    {
    }

    public override DocumentStorageMode Mode => DocumentStorageMode.GraphApiReady;

    public override async Task<DocumentStorageInfo> SaveDocumentAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken)
    {
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
        var url = BuildGraphReadyUrl(fileKey);

        return await SaveMetadataAsync(
            fileKey,
            safeName,
            string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            Mode,
            request.Purpose,
            DocumentStorageStatus.GraphApiReady,
            request.Module,
            request.EntityType,
            request.EntityId,
            request.FaenaCodigo,
            request.ActivoCodigo,
            request.OtNumero,
            relativeFolder,
            null,
            url,
            request.Content.LongLength,
            request.UploadedBy,
            request.Metadata,
            cancellationToken);
    }

    public override async Task<DocumentStorageInfo> SaveManualLinkAsync(
        ManualDocumentLinkRequest request,
        CancellationToken cancellationToken)
    {
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

    private string BuildGraphReadyUrl(string fileKey)
    {
        if (!string.IsNullOrWhiteSpace(Options.SiteUrl))
        {
            return $"{Options.SiteUrl.TrimEnd('/')}/{fileKey}";
        }

        return $"graph-ready://{fileKey}";
    }
}
