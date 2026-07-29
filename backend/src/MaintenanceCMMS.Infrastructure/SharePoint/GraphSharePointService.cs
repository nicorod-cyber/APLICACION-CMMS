using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.SharePoint;

public sealed class GraphSharePointService : SharePointStorageBase
{
    private static readonly HttpClient HttpClient = new();

    public GraphSharePointService(CmmsDbContext dbContext, IAuditService auditService, IOptions<SharePointOptions> options)
        : base(dbContext, auditService, options.Value) { }

    public override DocumentStorageMode Mode => DocumentStorageMode.GraphApiReady;
    protected override bool SupportsUpload => true;

    public override async Task<DocumentStorageInfo> SaveDocumentAsync(DocumentStorageSaveRequest request, CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(request.FileName, nameof(request.FileName));
        if (request.Content.Length == 0) throw new DomainException("El documento no contiene bytes para guardar.");
        EnsureConfigured();
        var relativeFolder = BuildRelativeFolder(new DocumentStoragePathRequest(request.Module, request.EntityType, request.EntityId, request.Purpose, request.FaenaCodigo, request.ActivoCodigo, request.OtNumero));
        var errors = ValidateRelativePath(relativeFolder);
        if (errors.Count > 0) throw new DomainException($"Ruta SharePoint invalida: {string.Join("; ", errors)}");
        var safeName = SanitizeFileName(request.FileName);
        var fileKey = BuildUniqueFileKey(relativeFolder, safeName);
        var remotePath = BuildRemotePath(fileKey);
        GraphItem? item = null;
        try
        {
            item = request.Content.Length <= Math.Max(1, Options.SimpleUploadMaxBytes)
                ? await UploadSmallAsync(remotePath, request.Content, request.ContentType, cancellationToken)
                : await UploadLargeAsync(remotePath, request.Content, cancellationToken);
            return await SaveMetadataAsync(fileKey, safeName, string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType, Mode, request.Purpose, DocumentStorageStatus.Stored, request.Module, request.EntityType, request.EntityId, request.FaenaCodigo, request.ActivoCodigo, request.OtNumero, relativeFolder, null, item.WebUrl ?? BuildContentUrl(remotePath), request.Content.LongLength, request.UploadedBy, request.Metadata, cancellationToken, ComputeChecksum(request.Content));
        }
        catch
        {
            if (item is not null) await TryDeleteRemoteAsync(remotePath, cancellationToken);
            throw;
        }
    }

    public override async Task<DocumentStorageInfo> SaveManualLinkAsync(ManualDocumentLinkRequest request, CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(request.Url, nameof(request.Url));
        var relativeFolder = BuildRelativeFolder(new DocumentStoragePathRequest(request.Module, request.EntityType, request.EntityId, request.Purpose, request.FaenaCodigo, request.ActivoCodigo, request.OtNumero));
        var safeName = SanitizeFileName(request.FileName);
        var fileKey = BuildUniqueFileKey(relativeFolder, safeName);
        return await SaveMetadataAsync(fileKey, safeName, "text/uri-list", Mode, request.Purpose, DocumentStorageStatus.ManualLink, request.Module, request.EntityType, request.EntityId, request.FaenaCodigo, request.ActivoCodigo, request.OtNumero, relativeFolder, null, request.Url, 0, request.LinkedBy, request.Metadata, cancellationToken);
    }

    public override async Task<DocumentStorageDownload?> DownloadAsync(string fileKey, CancellationToken cancellationToken)
    {
        var item = await GetAsync(fileKey, cancellationToken);
        if (item is null || item.Status is DocumentStorageStatus.Deleted or DocumentStorageStatus.ManualLink) return null;
        EnsureConfigured();
        var client = HttpClient;
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildContentUrl(BuildRemotePath(item.FileKey)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, "descargar el archivo desde Microsoft Graph", cancellationToken);
        return new DocumentStorageDownload(item.FileName, item.ContentType, await response.Content.ReadAsByteArrayAsync(cancellationToken));
    }

    protected override async Task<bool> DeletePhysicalContentAsync(FileMetadataEntity file, CancellationToken cancellationToken) =>
        file.MimeType == "text/uri-list" ? false : await TryDeleteRemoteAsync(BuildRemotePath(file.FileKey), cancellationToken);

    private async Task<GraphItem> UploadSmallAsync(string remotePath, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        var client = HttpClient;
        using var request = new HttpRequestMessage(HttpMethod.Put, BuildContentUrl(remotePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "cargar el archivo en Microsoft Graph", cancellationToken);
        return await ReadGraphItemAsync(response, cancellationToken);
    }

    private async Task<GraphItem> UploadLargeAsync(string remotePath, byte[] content, CancellationToken cancellationToken)
    {
        var client = HttpClient;
        using var sessionRequest = new HttpRequestMessage(HttpMethod.Post, BuildItemUrl(remotePath) + ":/createUploadSession");
        sessionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        sessionRequest.Content = JsonContent.Create(new { item = new Dictionary<string, string> { ["@microsoft.graph.conflictBehavior"] = "rename" } });
        using var sessionResponse = await client.SendAsync(sessionRequest, cancellationToken);
        await EnsureSuccessAsync(sessionResponse, "crear la sesion de carga de Microsoft Graph", cancellationToken);
        using var sessionJson = JsonDocument.Parse(await sessionResponse.Content.ReadAsStreamAsync(cancellationToken));
        var uploadUrl = sessionJson.RootElement.TryGetProperty("uploadUrl", out var uploadUrlElement) ? uploadUrlElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(uploadUrl)) throw new DomainException("Microsoft Graph no devolvio una URL de carga.");
        var chunkSize = Math.Max(320 * 1024, Options.UploadChunkBytes); chunkSize -= chunkSize % (320 * 1024); if (chunkSize == 0) chunkSize = 320 * 1024;
        GraphItem? completed = null;
        for (var offset = 0; offset < content.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, content.Length - offset);
            using var chunk = new ByteArrayContent(content, offset, length);
            chunk.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + length - 1, content.Length);
            using var response = await client.PutAsync(uploadUrl, chunk, cancellationToken);
            await EnsureSuccessAsync(response, "enviar un bloque a Microsoft Graph", cancellationToken);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created) completed = await ReadGraphItemAsync(response, cancellationToken);
        }
        return completed ?? throw new DomainException("Microsoft Graph no confirmo la carga completa del archivo.");
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var client = HttpClient;
        var tokenUrl = $"https://login.microsoftonline.com/{Uri.EscapeDataString(Options.TenantId)}/oauth2/v2.0/token";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = Options.ClientId, ["client_secret"] = Options.ClientSecret, ["scope"] = "https://graph.microsoft.com/.default", ["grant_type"] = "client_credentials" });
        using var response = await client.PostAsync(tokenUrl, content, cancellationToken);
        await EnsureSuccessAsync(response, "obtener un token para Microsoft Graph", cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var token = json.RootElement.TryGetProperty("access_token", out var element) ? element.GetString() : null;
        return string.IsNullOrWhiteSpace(token) ? throw new DomainException("Microsoft Entra no devolvio un token de acceso.") : token;
    }

    private async Task<bool> TryDeleteRemoteAsync(string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            EnsureConfigured();
            var client = HttpClient;
            using var request = new HttpRequestMessage(HttpMethod.Delete, BuildItemUrl(remotePath));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
            using var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
        }
        catch { return false; }
    }

    private static async Task<GraphItem> ReadGraphItemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return new GraphItem(json.RootElement.TryGetProperty("webUrl", out var webUrl) ? webUrl.GetString() : null);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new DomainException($"No fue posible {operation}. Microsoft Graph respondio {(int)response.StatusCode}: {detail[..Math.Min(500, detail.Length)]}");
    }

    private string BuildRemotePath(string fileKey) { var root = Options.BaseFolder.Trim().Trim('/'); return string.IsNullOrWhiteSpace(root) ? fileKey.Trim('/') : $"{root}/{fileKey.Trim('/')}"; }
    private string BuildContentUrl(string remotePath) => BuildItemUrl(remotePath) + ":/content";
    private string BuildItemUrl(string remotePath)
    {
        var graphBase = Options.GraphBaseUrl.TrimEnd('/');
        var path = string.Join('/', remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        return $"{graphBase}/sites/{Uri.EscapeDataString(Options.SiteId)}/drives/{Uri.EscapeDataString(Options.DriveId)}/root:/{path}";
    }
    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(Options.TenantId) || string.IsNullOrWhiteSpace(Options.ClientId) || string.IsNullOrWhiteSpace(Options.ClientSecret) || string.IsNullOrWhiteSpace(Options.SiteId) || string.IsNullOrWhiteSpace(Options.DriveId)) throw new DomainException("El proveedor Microsoft Graph requiere TenantId, ClientId, ClientSecret, SiteId y DriveId.");
    }
    private sealed record GraphItem(string? WebUrl);
}
