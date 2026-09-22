using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Infrastructure.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Api.Security;

public sealed class FileAccessPolicy(CmmsDbContext db, IAuthorizationPolicyService authorization, IWorkOrderService workOrders, IDocumentService documents)
{
    public async Task<bool> CanReadAsync(DocumentStorageInfo file, UserAccessContext user, CancellationToken ct)
    {
        if (authorization.CanAdminister(user)) return true;
        var documentIds = await db.DocumentVersions.Where(v => v.File.FileKey == file.FileKey).Select(v => v.DocumentId).Distinct().ToArrayAsync(ct);
        if (documentIds.Length > 0)
        {
            foreach (var id in documentIds)
            {
                try { if (await documents.GetByIdAsync(id.ToString("D"), user, ct) is not null) return true; }
                catch (UnauthorizedAccessException) { }
            }
            return false;
        }
        return await CanAccessTargetAsync(file.EntityType, file.EntityId, file.FaenaCodigo, file.ActivoCodigo, file.OtNumero, user, ct)
            || (string.IsNullOrWhiteSpace(file.FaenaCodigo) && string.IsNullOrWhiteSpace(file.ActivoCodigo) && string.IsNullOrWhiteSpace(file.OtNumero) && file.CreatedBy == user.UserId);
    }

    public async Task<bool> CanAccessTargetAsync(string entityType, string entityId, string? faena, string? asset, string? ot, UserAccessContext user, CancellationToken ct)
    {
        if (authorization.CanAdminister(user)) return true;
        if (!string.IsNullOrWhiteSpace(faena) && !authorization.CanViewFaena(user, faena)) return false;
        if (entityType.Equals("Activo", StringComparison.OrdinalIgnoreCase) || entityType.Equals("Asset", StringComparison.OrdinalIgnoreCase)) asset = entityId;
        if (entityType.Equals("OT", StringComparison.OrdinalIgnoreCase) || entityType.Equals("WorkOrder", StringComparison.OrdinalIgnoreCase)) ot = entityId;
        var resolved = false;
        if (!string.IsNullOrWhiteSpace(asset))
        {
            var actual = await db.Assets.Where(a => a.Code == asset).Select(a => new { Faena = a.Faena == null ? null : a.Faena.Code }).SingleOrDefaultAsync(ct);
            if (actual?.Faena is null || !authorization.CanViewFaena(user, actual.Faena)) return false;
            resolved = true;
        }
        if (!string.IsNullOrWhiteSpace(ot))
        {
            try { if (await workOrders.GetByIdAsync(ot, user, ct) is null) return false; }
            catch (UnauthorizedAccessException) { return false; }
            resolved = true;
        }
        if (entityType.Equals("Faena", StringComparison.OrdinalIgnoreCase))
            return authorization.CanViewFaena(user, entityId) && await db.Faenas.AnyAsync(f => f.Code == entityId, ct);
        if (entityType.Equals("Document", StringComparison.OrdinalIgnoreCase) && !resolved)
        {
            try { return await documents.GetByIdAsync(entityId, user, ct) is not null; }
            catch (UnauthorizedAccessException) { return false; }
        }
        return resolved;
    }
}

public sealed class SharePointAccessFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var context = invocation.HttpContext;
        var user = UserAccessContext.FromClaims(context.User);
        var authorization = context.RequestServices.GetRequiredService<IAuthorizationPolicyService>();
        var access = context.RequestServices.GetRequiredService<FileAccessPolicy>();
        var storage = context.RequestServices.GetRequiredService<IDocumentStorageService>();
        var ct = context.RequestAborted;
        var key = context.Request.Query["fileKey"].FirstOrDefault();
        if (key is not null)
        {
            var file = await storage.GetAsync(key, ct);
            if (file is null) return Results.NotFound();
            if (!await access.CanReadAsync(file, user, ct)) return Results.Forbid();
        }
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            if (!authorization.CanManageDocuments(user)) return Results.Forbid();
            (string Type, string Id, string? Faena, string? Asset, string? Ot)? target = null;
            if (invocation.Arguments.OfType<SharePointFolderApiRequest>().FirstOrDefault() is { } folder)
                target = (folder.EntityType, folder.EntityId, folder.FaenaCodigo, folder.ActivoCodigo, folder.OtNumero);
            if (invocation.Arguments.OfType<SharePointManualLinkApiRequest>().FirstOrDefault() is { } manual)
                target = (manual.EntityType, manual.EntityId, manual.FaenaCodigo, manual.ActivoCodigo, manual.OtNumero);
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(ct);
                target = (form["entityType"].ToString(), form["entityId"].ToString(), form["faenaCodigo"].FirstOrDefault(), form["activoCodigo"].FirstOrDefault(), form["otNumero"].FirstOrDefault());
            }
            if (target is { } t && !await access.CanAccessTargetAsync(t.Type, t.Id, t.Faena, t.Asset, t.Ot, user, ct)) return Results.Forbid();
        }
        var result = await next(invocation);
        if (result is IValueHttpResult { Value: IReadOnlyCollection<DocumentStorageInfo> files })
        {
            var visible = new List<DocumentStorageInfo>();
            foreach (var file in files) if (await access.CanReadAsync(file, user, ct)) visible.Add(file with { LocalPath = null });
            return Results.Ok(visible);
        }
        if (result is IValueHttpResult { Value: DocumentStorageInfo saved }) return Results.Ok(saved with { LocalPath = null });
        if (result is IValueHttpResult { Value: DocumentStorageFolderInfo created }) return Results.Ok(created with { LocalPath = null });
        return result;
    }
}
