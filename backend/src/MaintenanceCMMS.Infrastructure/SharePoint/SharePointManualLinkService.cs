using MaintenanceCMMS.Infrastructure.Data.SqlServer;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.SharePoint;

public sealed class SharePointManualLinkService : SharePointStorageBase
{
    public SharePointManualLinkService(
        CmmsDbContext dbContext,
        IAuditService auditService,
        IOptions<SharePointOptions> options)
        : base(dbContext, auditService, options.Value)
    {
    }

    public override DocumentStorageMode Mode => DocumentStorageMode.ManualLink;

    protected override bool RequiresManualLink => true;

    public override async Task<DocumentStorageInfo> SaveDocumentAsync(
        DocumentStorageSaveRequest request,
        CancellationToken cancellationToken)
    {
        UploadPolicy.Validate(request.FileName, request.ContentType, request.Content, request.Purpose == DocumentStoragePurpose.Evidence, request.EntityType == "WorkOrderSignature" ? 2 * 1024 * 1024 : request.Purpose == DocumentStoragePurpose.Evidence ? 10 * 1024 * 1024 : UploadPolicy.MaximumBytes);
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
        var url = string.IsNullOrWhiteSpace(Options.ManualRootUrl)
            ? string.Empty
            : $"{Options.ManualRootUrl.TrimEnd('/')}/{relativeFolder}/{safeName}";

        return await SaveMetadataAsync(
            fileKey,
            safeName,
            string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            Mode,
            request.Purpose,
            DocumentStorageStatus.PendingManualLink,
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
        DocumentUrlPolicy.RequireHttps(request.Url, Options.AllowedHosts);
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("http" or "https"))
        {
            throw new DomainException("El enlace SharePoint debe ser una URL http o https.");
        }

        var relativeFolder = BuildRelativeFolder(new DocumentStoragePathRequest(
            request.Module,
            request.EntityType,
            request.EntityId,
            request.Purpose,
            request.FaenaCodigo,
            request.ActivoCodigo,
            request.OtNumero));
        var safeName = SanitizeFileName(string.IsNullOrWhiteSpace(request.FileName) ? Path.GetFileName(parsed.LocalPath) : request.FileName);
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
