using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Common.ValueObjects;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Domain.Inventory;

public sealed class Warehouse : AuditableEntity
{
    public Warehouse(string name, EntityId faenaId)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        Name = name.Trim();
        FaenaId = faenaId;
    }

    public string Name { get; private set; }

    public EntityId FaenaId { get; private set; }
}

public sealed class WarehouseLocation : AuditableEntity
{
    public WarehouseLocation(EntityId warehouseId, string name, EntityCode code)
    {
        DomainGuard.AgainstEmpty(name, nameof(name));
        WarehouseId = warehouseId;
        Name = name.Trim();
        Code = code;
    }

    public EntityId WarehouseId { get; private set; }

    public string Name { get; private set; }

    public EntityCode Code { get; private set; }
}

/// <summary>
/// Spare part master data. SAP code is optional, but must be unique when present.
/// </summary>
public sealed class SparePart : AuditableEntity
{
    public SparePart(EntityCode code, string description, string? sapCode = null)
    {
        DomainGuard.AgainstEmpty(description, nameof(description));
        Code = code;
        Description = description.Trim();
        SapCode = string.IsNullOrWhiteSpace(sapCode) ? null : sapCode.Trim().ToUpperInvariant();
    }

    public EntityCode Code { get; private set; }

    public string Description { get; private set; }

    public string? SapCode { get; private set; }

    public static void EnsureUniqueSapCodes(IEnumerable<SparePart> spareParts)
    {
        var duplicates = spareParts
            .Where(part => !string.IsNullOrWhiteSpace(part.SapCode))
            .GroupBy(part => part.SapCode)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new DomainException($"Spare part SAP code must be unique. Duplicates: {string.Join(", ", duplicates)}.");
        }
    }
}

public sealed class SparePartFamilyCompatibility : AuditableEntity
{
    public SparePartFamilyCompatibility(EntityId sparePartId, EntityId assetFamilyId)
    {
        SparePartId = sparePartId;
        AssetFamilyId = assetFamilyId;
    }

    public EntityId SparePartId { get; private set; }

    public EntityId AssetFamilyId { get; private set; }
}

/// <summary>
/// Warehouse stock aggregate. Physical stock is changed only through stock movements.
/// </summary>
public sealed class StockItem : AuditableEntity
{
    public StockItem(EntityId warehouseId, EntityId sparePartId, decimal physicalQuantity = 0)
    {
        DomainGuard.AgainstNegative(physicalQuantity, nameof(physicalQuantity));
        WarehouseId = warehouseId;
        SparePartId = sparePartId;
        PhysicalQuantity = physicalQuantity;
    }

    public EntityId WarehouseId { get; private set; }

    public EntityId SparePartId { get; private set; }

    public decimal PhysicalQuantity { get; private set; }

    public decimal ReservedQuantity { get; private set; }

    public decimal AvailableQuantity => PhysicalQuantity - ReservedQuantity;

    public void Reserve(decimal quantity, EntityId workOrderId, string userId)
    {
        DomainGuard.AgainstNegative(quantity, nameof(quantity));

        if (quantity == 0)
        {
            throw new DomainException("Reservation quantity must be greater than zero.");
        }

        if (quantity > AvailableQuantity)
        {
            throw new DomainException("Reservation quantity exceeds available stock.");
        }

        ReservedQuantity += quantity;
        Touch(userId);
    }

    public void ApplyMovement(StockMovement movement, string userId)
    {
        if (movement.SparePartId != SparePartId)
        {
            throw new DomainException("Stock movement spare part does not match stock item.");
        }

        switch (movement.Type)
        {
            case StockMovementType.Reception:
            case StockMovementType.TransferIn:
            case StockMovementType.InTransit:
            case StockMovementType.TransferReception:
            case StockMovementType.ReturnFromWorkOrder:
            case StockMovementType.PositiveAdjustment:
            case StockMovementType.Adjustment:
            case StockMovementType.CountCorrection:
                PhysicalQuantity += movement.Quantity;
                break;
            case StockMovementType.MaintenanceConsumption:
            case StockMovementType.TransferOut:
            case StockMovementType.NegativeAdjustment:
            case StockMovementType.MaterialWriteOff:
                if (movement.Quantity > AvailableQuantity)
                {
                    throw new DomainException("Movement quantity exceeds available stock.");
                }

                PhysicalQuantity -= movement.Quantity;
                break;
            case StockMovementType.Reservation:
                Reserve(movement.Quantity, movement.WorkOrderId ?? EntityId.New(), userId);
                break;
            case StockMovementType.ReservationRelease:
                ReservedQuantity = Math.Max(0, ReservedQuantity - movement.Quantity);
                break;
            default:
                throw new DomainException("Unsupported stock movement type.");
        }

        Touch(userId);
    }
}

/// <summary>
/// Auditable stock movement. Maintenance consumption must be tied to an asset or work order.
/// </summary>
public sealed class StockMovement : AuditableEntity
{
    public StockMovement(
        StockMovementType type,
        EntityId sparePartId,
        decimal quantity,
        EntityId? assetId = null,
        EntityId? workOrderId = null,
        EntityId? sourceWarehouseId = null,
        EntityId? targetWarehouseId = null)
    {
        DomainGuard.AgainstNegative(quantity, nameof(quantity));

        if (quantity == 0)
        {
            throw new DomainException("Movement quantity must be greater than zero.");
        }

        if (type == StockMovementType.MaintenanceConsumption && !assetId.HasValue && !workOrderId.HasValue)
        {
            throw new DomainException("Maintenance consumption must reference an asset or work order.");
        }

        Type = type;
        SparePartId = sparePartId;
        Quantity = quantity;
        AssetId = assetId;
        WorkOrderId = workOrderId;
        SourceWarehouseId = sourceWarehouseId;
        TargetWarehouseId = targetWarehouseId;
    }

    public StockMovementType Type { get; private set; }

    public EntityId SparePartId { get; private set; }

    public decimal Quantity { get; private set; }

    public EntityId? AssetId { get; private set; }

    public EntityId? WorkOrderId { get; private set; }

    public EntityId? SourceWarehouseId { get; private set; }

    public EntityId? TargetWarehouseId { get; private set; }

    public static StockMovement CreateMaintenanceConsumption(EntityId sparePartId, decimal quantity, EntityId? assetId, EntityId? workOrderId)
    {
        return new StockMovement(StockMovementType.MaintenanceConsumption, sparePartId, quantity, assetId, workOrderId);
    }
}

public sealed class StockReservation : AuditableEntity
{
    public StockReservation(EntityId stockItemId, EntityId workOrderId, decimal quantity)
    {
        DomainGuard.AgainstNegative(quantity, nameof(quantity));
        StockItemId = stockItemId;
        WorkOrderId = workOrderId;
        Quantity = quantity;
    }

    public EntityId StockItemId { get; private set; }

    public EntityId WorkOrderId { get; private set; }

    public decimal Quantity { get; private set; }
}

public sealed class InventoryAdjustment : AuditableEntity
{
    public InventoryAdjustment(EntityId stockItemId, decimal quantity, string reason)
    {
        DomainGuard.AgainstEmpty(reason, nameof(reason));
        StockItemId = stockItemId;
        Quantity = quantity;
        Reason = reason.Trim();
    }

    public EntityId StockItemId { get; private set; }

    public decimal Quantity { get; private set; }

    public string Reason { get; private set; }
}

public sealed class InventoryCount : AuditableEntity
{
    public InventoryCount(EntityId warehouseId, DateOnly countDate)
    {
        WarehouseId = warehouseId;
        CountDate = countDate;
    }

    public EntityId WarehouseId { get; private set; }

    public DateOnly CountDate { get; private set; }
}

public sealed class WarehouseTransfer : AuditableEntity
{
    public WarehouseTransfer(EntityId sourceWarehouseId, EntityId targetWarehouseId, EntityId sparePartId, decimal quantity)
    {
        DomainGuard.AgainstNegative(quantity, nameof(quantity));
        SourceWarehouseId = sourceWarehouseId;
        TargetWarehouseId = targetWarehouseId;
        SparePartId = sparePartId;
        Quantity = quantity;
    }

    public EntityId SourceWarehouseId { get; private set; }

    public EntityId TargetWarehouseId { get; private set; }

    public EntityId SparePartId { get; private set; }

    public decimal Quantity { get; private set; }
}
