using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Domain.PreventiveMaintenance;

public enum PreventivePlanFrequency
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4,
    MeterBased = 5
}

public sealed class PreventivePlan : AuditableEntity
{
    public PreventivePlan(EntityId assetId, string name, PreventivePlanFrequency frequency, MaintenanceType maintenanceType)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        AssetId = assetId;
        Name = name.Trim();
        Frequency = frequency;
        MaintenanceType = maintenanceType;
    }

    public EntityId AssetId { get; private set; }

    public string Name { get; private set; }

    public PreventivePlanFrequency Frequency { get; private set; }

    public MaintenanceType MaintenanceType { get; private set; }

    public bool IsActive { get; private set; } = true;
}

public sealed class PreventivePlanChecklist : AuditableEntity
{
    public PreventivePlanChecklist(EntityId preventivePlanId, string name)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        PreventivePlanId = preventivePlanId;
        Name = name.Trim();
    }

    public EntityId PreventivePlanId { get; private set; }

    public string Name { get; private set; }
}

public sealed class PreventiveTrigger : AuditableEntity
{
    public PreventiveTrigger(EntityId preventivePlanId, DateOnly nextDueDate, decimal? meterThreshold = null)
    {
        PreventivePlanId = preventivePlanId;
        NextDueDate = nextDueDate;
        MeterThreshold = meterThreshold;
    }

    public EntityId PreventivePlanId { get; private set; }

    public DateOnly NextDueDate { get; private set; }

    public decimal? MeterThreshold { get; private set; }
}

public sealed class MeterReading : AuditableEntity
{
    public MeterReading(EntityId assetId, string meterName, decimal value, DateTimeOffset measuredAt)
    {
        DomainGuard.AgainstEmpty(meterName, nameof(meterName));
        AssetId = assetId;
        MeterName = meterName.Trim();
        Value = value;
        MeasuredAt = measuredAt;
    }

    public EntityId AssetId { get; private set; }

    public string MeterName { get; private set; }

    public decimal Value { get; private set; }

    public DateTimeOffset MeasuredAt { get; private set; }
}

