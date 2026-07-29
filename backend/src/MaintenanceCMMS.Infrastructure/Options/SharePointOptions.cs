namespace MaintenanceCMMS.Infrastructure.Options;

public sealed class SharePointOptions
{
    public string Provider { get; init; } = "LocalSimulation";
    public string LocalPath { get; init; } = "data/sharepoint-simulated";
    public string ManualRootUrl { get; init; } = string.Empty;
    public string SiteUrl { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string SiteId { get; init; } = string.Empty;
    public string DriveId { get; init; } = string.Empty;
    public string BaseFolder { get; init; } = "CMMS";
    public string GraphBaseUrl { get; init; } = "https://graph.microsoft.com/v1.0";
    public int SimpleUploadMaxBytes { get; init; } = 4 * 1024 * 1024;
    public int UploadChunkBytes { get; init; } = 5 * 1024 * 1024;
}
