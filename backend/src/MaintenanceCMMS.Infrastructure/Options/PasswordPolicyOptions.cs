namespace MaintenanceCMMS.Infrastructure.Options;

public sealed class PasswordPolicyOptions
{
    public int MinimumLength { get; init; } = 12;
    public int MaximumLength { get; init; } = 128;
}
