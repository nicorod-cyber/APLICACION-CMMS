namespace MaintenanceCMMS.Domain.Common;

public abstract class AuditableEntity : Entity
{
    protected AuditableEntity()
        : base()
    {
    }

    protected AuditableEntity(EntityId id)
        : base(id)
    {
    }

    public DateTimeOffset CreatedAt { get; protected init; } = DateTimeOffset.UtcNow;

    public string CreatedBy { get; protected init; } = "system";

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    protected void Touch(string userId)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = string.IsNullOrWhiteSpace(userId) ? "system" : userId;
    }
}

