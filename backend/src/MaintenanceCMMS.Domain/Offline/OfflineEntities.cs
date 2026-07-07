using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Domain.Offline;

public sealed class OfflineSyncPackage : AuditableEntity
{
    public OfflineSyncPackage(EntityId userId, EntityId faenaId)
    {
        UserId = userId;
        FaenaId = faenaId;
        Status = SyncStatus.Pending;
    }

    public EntityId UserId { get; private set; }

    public EntityId FaenaId { get; private set; }

    public SyncStatus Status { get; private set; }
}

public sealed class OfflineSyncItem : AuditableEntity
{
    public OfflineSyncItem(EntityId offlineSyncPackageId, string entityName, EntityId entityId, string payload)
    {
        DomainGuard.AgainstEmpty(entityName, nameof(entityName));
        DomainGuard.AgainstEmpty(payload, nameof(payload));
        OfflineSyncPackageId = offlineSyncPackageId;
        EntityName = entityName.Trim();
        EntityId = entityId;
        Payload = payload;
        Status = SyncStatus.Pending;
    }

    public EntityId OfflineSyncPackageId { get; private set; }

    public string EntityName { get; private set; }

    public EntityId EntityId { get; private set; }

    public string Payload { get; private set; }

    public SyncStatus Status { get; private set; }
}

public sealed class SyncConflict : AuditableEntity
{
    public SyncConflict(EntityId offlineSyncItemId, string serverValue, string clientValue)
    {
        OfflineSyncItemId = offlineSyncItemId;
        ServerValue = serverValue;
        ClientValue = clientValue;
    }

    public EntityId OfflineSyncItemId { get; private set; }

    public string ServerValue { get; private set; }

    public string ClientValue { get; private set; }

    public bool Resolved { get; private set; }
}

