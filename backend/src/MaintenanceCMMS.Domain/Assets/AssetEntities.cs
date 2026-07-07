using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Common.ValueObjects;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Domain.Assets;

/// <summary>
/// Master asset tracked by maintenance, documentation, work orders, availability and costs.
/// </summary>
public sealed class Asset : AuditableEntity
{
    public Asset(EntityCode code, string name, EntityId faenaId, EntityId assetTypeId)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        Code = code;
        Name = name.Trim();
        FaenaId = faenaId;
        AssetTypeId = assetTypeId;
        Status = AssetStatus.Active;
    }

    public EntityCode Code { get; private set; }

    public string Name { get; private set; }

    public EntityId FaenaId { get; private set; }

    public EntityId AssetTypeId { get; private set; }

    public AssetStatus Status { get; private set; }

    public bool IsAvailabilityBlockedByDocuments(IEnumerable<Documents.AssetDocument> documents, DateOnly referenceDate)
    {
        return documents.Any(document => document.AssetId == Id && document.BlocksAvailability(referenceDate));
    }
}

public sealed class AssetFamily : AuditableEntity
{
    public AssetFamily(string name)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        Name = name.Trim();
    }

    public string Name { get; private set; }
}

public sealed class AssetType : AuditableEntity
{
    public AssetType(EntityId familyId, string name)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        FamilyId = familyId;
        Name = name.Trim();
    }

    public EntityId FamilyId { get; private set; }

    public string Name { get; private set; }
}

public sealed class AssetPropertyType : AuditableEntity
{
    public AssetPropertyType(string name, string dataType)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        DomainGuard.AgainstEmpty(dataType, nameof(dataType));
        Name = name.Trim();
        DataType = dataType.Trim();
    }

    public string Name { get; private set; }

    public string DataType { get; private set; }
}

public sealed class AssetTechnicalRecord : AuditableEntity
{
    public AssetTechnicalRecord(EntityId assetId, EntityId propertyTypeId, string value)
    {
        DomainGuard.AgainstEmpty(value, nameof(value));
        AssetId = assetId;
        PropertyTypeId = propertyTypeId;
        Value = value.Trim();
    }

    public EntityId AssetId { get; private set; }

    public EntityId PropertyTypeId { get; private set; }

    public string Value { get; private set; }
}

public sealed class AssetStateEvent : AuditableEntity
{
    public AssetStateEvent(EntityId assetId, AssetStatus status, DateTimeOffset occurredAt, string reason)
    {
        DomainGuard.AgainstEmpty(reason, nameof(reason));
        AssetId = assetId;
        Status = status;
        OccurredAt = occurredAt;
        Reason = reason.Trim();
    }

    public EntityId AssetId { get; private set; }

    public AssetStatus Status { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Reason { get; private set; }
}

public sealed class AssetAvailabilityEvent : AuditableEntity
{
    public AssetAvailabilityEvent(EntityId assetId, DateTimeOffset start, string cause)
    {
        DomainGuard.AgainstEmpty(cause, nameof(cause));
        AssetId = assetId;
        Start = start;
        Cause = cause.Trim();
    }

    public EntityId AssetId { get; private set; }

    public DateTimeOffset Start { get; private set; }

    public DateTimeOffset? End { get; private set; }

    public string Cause { get; private set; }
}

public static class AssetMasterRules
{
    public static void EnsureUniqueAssetCodes(IEnumerable<Asset> assets)
    {
        var duplicates = assets
            .GroupBy(asset => asset.Code.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new DomainException($"Asset code must be unique. Duplicates: {string.Join(", ", duplicates)}.");
        }
    }
}

