using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Domain.Documents;

public sealed class DocumentType : AuditableEntity
{
    public DocumentType(string name, bool isCritical)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        Name = name.Trim();
        IsCritical = isCritical;
    }

    public string Name { get; private set; }

    public bool IsCritical { get; private set; }
}

/// <summary>
/// Document metadata related to an asset. Files live in SharePoint or the local simulator.
/// </summary>
public sealed class AssetDocument : AuditableEntity
{
    public AssetDocument(EntityId assetId, EntityId documentTypeId, string fileKey, DateOnly? expiresOn, bool isCritical)
    {
        DomainGuard.AgainstEmpty(fileKey, nameof(fileKey));
        AssetId = assetId;
        DocumentTypeId = documentTypeId;
        FileKey = fileKey.Trim();
        ExpiresOn = expiresOn;
        IsCritical = isCritical;
        Status = DocumentStatus.PendingValidation;
    }

    public EntityId AssetId { get; private set; }

    public EntityId DocumentTypeId { get; private set; }

    public string FileKey { get; private set; }

    public DateOnly? ExpiresOn { get; private set; }

    public bool IsCritical { get; private set; }

    public DocumentStatus Status { get; private set; }

    public bool IsValidated => Status == DocumentStatus.Validated;

    public bool IsExpired(DateOnly referenceDate) => ExpiresOn.HasValue && ExpiresOn.Value < referenceDate;

    public bool BlocksAvailability(DateOnly referenceDate) => IsCritical && IsExpired(referenceDate);

    public void Validate(string userId)
    {
        Status = DocumentStatus.Validated;
        Touch(userId);
    }

    public void UpdateCriticalMetadata(DateOnly? expiresOn, bool isCritical, string userId)
    {
        if (IsValidated)
        {
            throw new DomainException("Validated documents block critical field changes.");
        }

        ExpiresOn = expiresOn;
        IsCritical = isCritical;
        Touch(userId);
    }
}

public sealed class DocumentRequirement : AuditableEntity
{
    public DocumentRequirement(EntityId assetTypeId, EntityId documentTypeId, bool isMandatory, bool blocksAvailability)
    {
        AssetTypeId = assetTypeId;
        DocumentTypeId = documentTypeId;
        IsMandatory = isMandatory;
        BlocksAvailability = blocksAvailability;
    }

    public EntityId AssetTypeId { get; private set; }

    public EntityId DocumentTypeId { get; private set; }

    public bool IsMandatory { get; private set; }

    public bool BlocksAvailability { get; private set; }
}

public sealed class DocumentAlertRule : AuditableEntity
{
    public DocumentAlertRule(EntityId documentTypeId, int daysBeforeExpiration, AlertSeverity severity)
    {
        if (daysBeforeExpiration < 0)
        {
            throw new DomainException("Alert days before expiration cannot be negative.");
        }

        DocumentTypeId = documentTypeId;
        DaysBeforeExpiration = daysBeforeExpiration;
        Severity = severity;
    }

    public EntityId DocumentTypeId { get; private set; }

    public int DaysBeforeExpiration { get; private set; }

    public AlertSeverity Severity { get; private set; }
}

public sealed class DocumentValidation : AuditableEntity
{
    public DocumentValidation(EntityId assetDocumentId, string validatorUserId, bool approved, string? comments = null)
    {
        DomainGuard.AgainstEmpty(validatorUserId, nameof(validatorUserId));
        AssetDocumentId = assetDocumentId;
        ValidatorUserId = validatorUserId.Trim();
        Approved = approved;
        Comments = comments;
    }

    public EntityId AssetDocumentId { get; private set; }

    public string ValidatorUserId { get; private set; }

    public bool Approved { get; private set; }

    public string? Comments { get; private set; }
}

