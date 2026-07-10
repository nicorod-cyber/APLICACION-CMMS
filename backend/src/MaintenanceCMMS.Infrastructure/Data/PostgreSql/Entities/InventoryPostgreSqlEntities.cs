namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;

public sealed class InventoryCatalogEntity : PostgreSqlEntity
{
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class WarehouseEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid FaenaId { get; set; }
    public FaenaEntity Faena { get; set; } = null!;
    public Guid TypeId { get; set; }
    public InventoryCatalogEntity Type { get; set; } = null!;
    public string? Location { get; set; }
    public string? ResponsibleUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AllowsNegativeStock { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? UpdatedByUserId { get; set; }
    public List<WarehouseLocationEntity> Locations { get; set; } = [];
}

public sealed class WarehouseLocationEntity : PostgreSqlEntity
{
    public Guid WarehouseId { get; set; }
    public WarehouseEntity Warehouse { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Aisle { get; set; }
    public string? Shelf { get; set; }
    public string? Level { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SparePartEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string? SapCode { get; set; }
    public string? SupplierCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public string TechnicalDescription { get; set; } = string.Empty;
    public Guid UnitId { get; set; }
    public InventoryCatalogEntity Unit { get; set; } = null!;
    public Guid? CategoryId { get; set; }
    public InventoryCatalogEntity? Category { get; set; }
    public string? Manufacturer { get; set; }
    public string? ModelReference { get; set; }
    public bool IsCritical { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal MaximumStock { get; set; }
    public decimal ReorderPoint { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal? AverageUnitCost { get; set; }
    public string Status { get; set; } = "Activo";
    public string? PreferredSupplier { get; set; }
    public string? ReplacementCode { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? UpdatedByUserId { get; set; }
}

public sealed class WarehouseStockEntity : PostgreSqlEntity
{
    public Guid SparePartId { get; set; }
    public SparePartEntity SparePart { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public WarehouseEntity Warehouse { get; set; } = null!;
    public Guid? WarehouseLocationId { get; set; }
    public WarehouseLocationEntity? WarehouseLocation { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal? MinimumStockOverride { get; set; }
}

public sealed class StockMovementEntity : PostgreSqlEntity
{
    public string MovementNumber { get; set; } = string.Empty;
    public Guid MovementTypeId { get; set; }
    public InventoryCatalogEntity MovementType { get; set; } = null!;
    public Guid SparePartId { get; set; }
    public SparePartEntity SparePart { get; set; } = null!;
    public decimal Quantity { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public WarehouseEntity? SourceWarehouse { get; set; }
    public Guid? TargetWarehouseId { get; set; }
    public WarehouseEntity? TargetWarehouse { get; set; }
    public Guid? ReservationId { get; set; }
    public StockReservationEntity? Reservation { get; set; }
    public Guid? TransferId { get; set; }
    public StockTransferEntity? Transfer { get; set; }
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public decimal PhysicalBefore { get; set; }
    public decimal PhysicalAfter { get; set; }
    public decimal ReservedBefore { get; set; }
    public decimal ReservedAfter { get; set; }
    public bool IsReversed { get; set; }
    public Guid? ReversalOfMovementId { get; set; }
}

public sealed class StockReservationEntity : PostgreSqlEntity
{
    public string ReservationNumber { get; set; } = string.Empty;
    public Guid SparePartId { get; set; }
    public SparePartEntity SparePart { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public WarehouseEntity Warehouse { get; set; } = null!;
    public decimal RequestedQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal ReleasedQuantity { get; set; }
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string Status { get; set; } = "Activa";
    public string Reason { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? ReleasedAtUtc { get; set; }
}

public sealed class StockTransferEntity : PostgreSqlEntity
{
    public string TransferNumber { get; set; } = string.Empty;
    public Guid SourceWarehouseId { get; set; }
    public WarehouseEntity SourceWarehouse { get; set; } = null!;
    public Guid TransitWarehouseId { get; set; }
    public WarehouseEntity TransitWarehouse { get; set; } = null!;
    public Guid TargetWarehouseId { get; set; }
    public WarehouseEntity TargetWarehouse { get; set; } = null!;
    public Guid SparePartId { get; set; }
    public SparePartEntity SparePart { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string Status { get; set; } = "EnTransito";
    public string Reason { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReceivedAtUtc { get; set; }
    public string? ReceivedByUserId { get; set; }
    public string? ReceptionReason { get; set; }
    public string? CancellationReason { get; set; }
}
