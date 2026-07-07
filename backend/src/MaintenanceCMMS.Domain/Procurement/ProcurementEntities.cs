using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Common.ValueObjects;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Domain.Inventory;

namespace MaintenanceCMMS.Domain.Procurement;

public sealed class SparePartRequest : AuditableEntity
{
    public SparePartRequest(EntityId requestedByUserId, EntityId faenaId)
    {
        RequestedByUserId = requestedByUserId;
        FaenaId = faenaId;
        Status = SparePartRequestStatus.Draft;
    }

    public EntityId RequestedByUserId { get; private set; }

    public EntityId FaenaId { get; private set; }

    public SparePartRequestStatus Status { get; private set; }
}

public sealed class SparePartRequestLine : AuditableEntity
{
    public SparePartRequestLine(EntityId sparePartRequestId, EntityId sparePartId, decimal quantity)
    {
        DomainGuard.AgainstNegative(quantity, nameof(quantity));
        SparePartRequestId = sparePartRequestId;
        SparePartId = sparePartId;
        Quantity = quantity;
    }

    public EntityId SparePartRequestId { get; private set; }

    public EntityId SparePartId { get; private set; }

    public decimal Quantity { get; private set; }
}

/// <summary>
/// Request for material that is not yet codified. It can become a spare part master after approval.
/// </summary>
public sealed class NonCodedMaterialRequest : AuditableEntity
{
    public NonCodedMaterialRequest(string description, decimal quantity)
    {
        DomainGuard.AgainstEmpty(description, nameof(description));
        DomainGuard.AgainstNegative(quantity, nameof(quantity));
        Description = description.Trim();
        Quantity = quantity;
    }

    public string Description { get; private set; }

    public decimal Quantity { get; private set; }

    public bool ConvertedToSparePart { get; private set; }

    public EntityId? ConvertedSparePartId { get; private set; }

    public SparePart ConvertToSparePart(EntityCode code, string? sapCode, string userId)
    {
        if (ConvertedToSparePart)
        {
            throw new DomainException("Non-coded material has already been converted.");
        }

        var sparePart = new SparePart(code, Description, sapCode);
        ConvertedToSparePart = true;
        ConvertedSparePartId = sparePart.Id;
        Touch(userId);

        return sparePart;
    }
}

public sealed class ProcurementRequest : AuditableEntity
{
    public ProcurementRequest(EntityId faenaId, string requestNumber)
    {
        DomainGuard.AgainstEmpty(requestNumber, nameof(requestNumber));
        FaenaId = faenaId;
        RequestNumber = requestNumber.Trim();
    }

    public EntityId FaenaId { get; private set; }

    public string RequestNumber { get; private set; }
}

public sealed class PurchaseOrderReference : AuditableEntity
{
    public PurchaseOrderReference(EntityId procurementRequestId, string purchaseOrderNumber)
    {
        DomainGuard.AgainstEmpty(purchaseOrderNumber, nameof(purchaseOrderNumber));
        ProcurementRequestId = procurementRequestId;
        PurchaseOrderNumber = purchaseOrderNumber.Trim();
    }

    public EntityId ProcurementRequestId { get; private set; }

    public string PurchaseOrderNumber { get; private set; }
}

public sealed class Supplier : AuditableEntity
{
    public Supplier(string name, string taxId)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        DomainGuard.AgainstEmpty(taxId, nameof(taxId));
        Name = name.Trim();
        TaxId = taxId.Trim();
    }

    public string Name { get; private set; }

    public string TaxId { get; private set; }
}

public sealed class LeadTimeTracking : AuditableEntity
{
    public LeadTimeTracking(EntityId supplierId, EntityId sparePartId, DateOnly requestedOn)
    {
        SupplierId = supplierId;
        SparePartId = sparePartId;
        RequestedOn = requestedOn;
    }

    public EntityId SupplierId { get; private set; }

    public EntityId SparePartId { get; private set; }

    public DateOnly RequestedOn { get; private set; }

    public DateOnly? ReceivedOn { get; private set; }

    public int? LeadTimeDays => ReceivedOn.HasValue ? ReceivedOn.Value.DayNumber - RequestedOn.DayNumber : null;
}

