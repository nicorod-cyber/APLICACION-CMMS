namespace MaintenanceCMMS.Infrastructure.Options;

public sealed class PowerBIOptions
{
    public string DatasetPrefix { get; init; } = "cmms";

    public bool UseReportingViewsOnly { get; init; } = true;
}

