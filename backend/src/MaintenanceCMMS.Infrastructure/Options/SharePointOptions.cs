namespace MaintenanceCMMS.Infrastructure.Options;

public sealed class SharePointOptions
{
    public string Provider { get; init; } = "LocalSimulation";

    public string LocalPath { get; init; } = "data/sharepoint-simulated";

    public string ManualRootUrl { get; init; } = string.Empty;

    public string SiteUrl { get; init; } = string.Empty;

    public string TenantId { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string SiteId { get; init; } = string.Empty;

    public string DriveId { get; init; } = string.Empty;
}
