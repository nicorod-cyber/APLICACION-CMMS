namespace MaintenanceCMMS.Infrastructure.Options;

public sealed class BootstrapDefaultsOptions
{
    public bool CreateDefaultAlertConfiguration { get; init; } = true;

    public bool CreateDefaultPdfTemplate { get; init; } = true;
}