namespace MaintenanceCMMS.Infrastructure.Options;

public sealed class OfflineOptions
{
    public bool Enabled { get; init; } = true;

    public int SyncWindowDays { get; init; } = 30;
}

