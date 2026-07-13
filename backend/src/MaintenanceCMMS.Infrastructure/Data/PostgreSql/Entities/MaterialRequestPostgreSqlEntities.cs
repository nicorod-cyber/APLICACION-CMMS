namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;

public sealed class MaterialRequestEntity : PostgreSqlEntity
{
    public string RequestNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Guid? FaenaId { get; set; }
    public FaenaEntity? Faena { get; set; }
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public Guid? AssetId { get; set; }
    public AssetEntity? Asset { get; set; }
    public Guid? WarehouseId { get; set; }
    public WarehouseEntity? Warehouse { get; set; }
    public string RequesterUserId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string TechnicalDescription { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? PhotoReference { get; set; }
    public string? TaskCode { get; set; }
    public string? StockDecision { get; set; }
    public string? MaintenanceApproverUserId { get; set; }
    public DateTimeOffset? MaintenanceApprovedAtUtc { get; set; }
    public string? WarehouseApproverUserId { get; set; }
    public DateTimeOffset? WarehouseApprovedAtUtc { get; set; }
    public string? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAtUtc { get; set; }
    public string? RejectReason { get; set; }
    public string? ReceivedByUserId { get; set; }
    public DateTimeOffset? ReceivedAtUtc { get; set; }
    public string? ConvertedByUserId { get; set; }
    public DateTimeOffset? ConvertedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? Observations { get; set; }
    public List<MaterialRequestItemEntity> Items { get; set; } = [];
    public List<MaterialRequestStatusHistoryEntity> History { get; set; } = [];
}

public sealed class MaterialRequestItemEntity : PostgreSqlEntity
{
    public Guid MaterialRequestId { get; set; }
    public MaterialRequestEntity MaterialRequest { get; set; } = null!;
    public Guid? SparePartId { get; set; }
    public SparePartEntity? SparePart { get; set; }
    public Guid? ReservationId { get; set; }
    public StockReservationEntity? Reservation { get; set; }
    public string? MasterSparePartCode { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal ApprovedQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? DeliveryMovementNumber { get; set; }
    public string? Observations { get; set; }
}

public sealed class MaterialRequestStatusHistoryEntity : PostgreSqlEntity
{
    public Guid MaterialRequestId { get; set; }
    public MaterialRequestEntity MaterialRequest { get; set; } = null!;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Reason { get; set; } = string.Empty;
}
