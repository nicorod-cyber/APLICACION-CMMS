namespace MaintenanceCMMS.Infrastructure.Options;

public sealed class MailOptions
{
    public string Provider { get; init; } = "Mailhog";

    public string From { get; init; } = "cmms@example.local";

    public string PlanningEmail { get; init; } = "planificacion@example.local";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 1025;
}
