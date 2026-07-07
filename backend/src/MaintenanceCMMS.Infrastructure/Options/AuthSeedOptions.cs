namespace MaintenanceCMMS.Infrastructure.Options;

public sealed class AuthSeedOptions
{
    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = "Administrador CMMS";

    public string Password { get; init; } = string.Empty;

    public string[] Faenas { get; init; } = [];
}
