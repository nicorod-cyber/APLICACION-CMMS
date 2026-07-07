namespace MaintenanceCMMS.Infrastructure.Options;

public sealed class PdfOptions
{
    public string TemplatePath { get; init; } = "data/templates";

    public string Renderer { get; init; } = "Html";
}

