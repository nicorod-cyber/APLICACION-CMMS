namespace MaintenanceCMMS.Domain.Common;

public abstract class Entity
{
    protected Entity()
        : this(EntityId.New())
    {
    }

    protected Entity(EntityId id)
    {
        Id = id;
    }

    public EntityId Id { get; protected init; }
}

