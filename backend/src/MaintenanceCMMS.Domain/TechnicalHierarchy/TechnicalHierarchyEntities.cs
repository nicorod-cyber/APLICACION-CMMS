using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Common.ValueObjects;

namespace MaintenanceCMMS.Domain.TechnicalHierarchy;

public sealed class TechnicalSystem : AuditableEntity
{
    public TechnicalSystem(string name, EntityCode code)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        Name = name.Trim();
        Code = code;
    }

    public string Name { get; private set; }

    public EntityCode Code { get; private set; }
}

public sealed class TechnicalSubsystem : AuditableEntity
{
    public TechnicalSubsystem(EntityId technicalSystemId, string name, EntityCode code)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        TechnicalSystemId = technicalSystemId;
        Name = name.Trim();
        Code = code;
    }

    public EntityId TechnicalSystemId { get; private set; }

    public string Name { get; private set; }

    public EntityCode Code { get; private set; }
}

public sealed class Component : AuditableEntity
{
    public Component(EntityId technicalSubsystemId, string name, EntityCode code)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        TechnicalSubsystemId = technicalSubsystemId;
        Name = name.Trim();
        Code = code;
    }

    public EntityId TechnicalSubsystemId { get; private set; }

    public string Name { get; private set; }

    public EntityCode Code { get; private set; }
}

public sealed class Subcomponent : AuditableEntity
{
    public Subcomponent(EntityId componentId, string name, EntityCode code)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        ComponentId = componentId;
        Name = name.Trim();
        Code = code;
    }

    public EntityId ComponentId { get; private set; }

    public string Name { get; private set; }

    public EntityCode Code { get; private set; }
}

public sealed class AssetComponentAssignment : AuditableEntity
{
    public AssetComponentAssignment(EntityId assetId, EntityId componentId)
    {
        AssetId = assetId;
        ComponentId = componentId;
    }

    public EntityId AssetId { get; private set; }

    public EntityId ComponentId { get; private set; }

    public EntityId? SubcomponentId { get; private set; }
}

