using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Domain.Security;

public sealed class User : AuditableEntity
{
    public User(string email, string displayName)
    {
        DomainGuard.AgainstEmpty(email, nameof(email));
        DomainGuard.AgainstEmpty(displayName, nameof(displayName));

        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
    }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    public bool IsActive { get; private set; } = true;
}

public sealed class Role : AuditableEntity
{
    public Role(string name, UserRoleType roleType)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        Name = name.Trim();
        RoleType = roleType;
    }

    public string Name { get; private set; }

    public UserRoleType RoleType { get; private set; }
}

public sealed class Permission : AuditableEntity
{
    public Permission(string code, string description)
    {
        DomainGuard.AgainstEmpty(code, nameof(code));
        DomainGuard.AgainstEmpty(description, nameof(description));
        Code = code.Trim().ToUpperInvariant();
        Description = description.Trim();
    }

    public string Code { get; private set; }

    public string Description { get; private set; }
}

public sealed class UserFaena : AuditableEntity
{
    public UserFaena(EntityId userId, EntityId faenaId)
    {
        UserId = userId;
        FaenaId = faenaId;
    }

    public EntityId UserId { get; private set; }

    public EntityId FaenaId { get; private set; }
}

public sealed class UserRole : AuditableEntity
{
    public UserRole(EntityId userId, EntityId roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    public EntityId UserId { get; private set; }

    public EntityId RoleId { get; private set; }
}

public sealed class AuditLog : AuditableEntity
{
    public AuditLog(string entityName, EntityId entityId, AuditAction action, string userId, string? before = null, string? after = null)
    {
        DomainGuard.AgainstEmpty(entityName, nameof(entityName));
        DomainGuard.AgainstEmpty(userId, nameof(userId));

        EntityName = entityName.Trim();
        EntityId = entityId;
        Action = action;
        UserId = userId.Trim();
        Before = before;
        After = after;
    }

    public string EntityName { get; private set; }

    public EntityId EntityId { get; private set; }

    public AuditAction Action { get; private set; }

    public string UserId { get; private set; }

    public string? Before { get; private set; }

    public string? After { get; private set; }
}

