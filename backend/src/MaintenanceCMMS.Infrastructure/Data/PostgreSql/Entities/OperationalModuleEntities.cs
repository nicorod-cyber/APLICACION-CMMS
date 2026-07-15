namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;

// These entities replace the former JSONB operational collections.  They keep
// operational facts in first-class tables so foreign keys and constraints are
// enforced by PostgreSQL rather than by application-side dictionaries.

public sealed class AvailabilityContractEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public Guid FaenaId { get; set; }
    public FaenaEntity Faena { get; set; } = null!;
    public decimal CommittedHoursPerDay { get; set; } = 24m;
    public decimal TargetAvailability { get; set; } = .9m;
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public string? ClientRules { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? UpdatedByUserId { get; set; }
    public List<AvailabilityContractAssignmentEntity> Assignments { get; set; } = [];
}

public sealed class AvailabilityContractAssignmentEntity : PostgreSqlEntity
{
    public Guid ContractId { get; set; }
    public AvailabilityContractEntity Contract { get; set; } = null!;
    public Guid? AssetId { get; set; }
    public AssetEntity? Asset { get; set; }
    public Guid? OperationalUnitId { get; set; }
    public OperationalUnitEntity? OperationalUnit { get; set; }
    public int Role { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
}

public sealed class AvailabilityEventEntity : PostgreSqlEntity
{
    public Guid ContractId { get; set; }
    public AvailabilityContractEntity Contract { get; set; } = null!;
    public Guid? ContractAssignmentId { get; set; }
    public AvailabilityContractAssignmentEntity? ContractAssignment { get; set; }
    public Guid? AssetId { get; set; }
    public AssetEntity? Asset { get; set; }
    public Guid? OperationalUnitId { get; set; }
    public OperationalUnitEntity? OperationalUnit { get; set; }
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public int Cause { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public bool CanBeUsed { get; set; }
    public bool IsMaintenanceAttributable { get; set; } = true;
    public string? Comment { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? UpdatedByUserId { get; set; }
}

public sealed class PreventivePlanEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int FrequencyType { get; set; }
    public decimal? FrequencyHours { get; set; }
    public decimal? FrequencyKilometers { get; set; }
    public int? FrequencyDays { get; set; }
    public decimal HourTolerance { get; set; }
    public decimal KilometerTolerance { get; set; }
    public int DayTolerance { get; set; }
    public Guid? ChecklistTemplateId { get; set; }
    public ChecklistTemplateEntity? ChecklistTemplate { get; set; }
    public string? SuggestedSpareParts { get; set; }
    public decimal EstimatedLaborHours { get; set; } = 1m;
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? LastExecutedAtUtc { get; set; }
    public decimal? LastExecutedHours { get; set; }
    public decimal? LastExecutedKilometers { get; set; }
    public DateTimeOffset? NextDueAtUtc { get; set; }
    public decimal? NextDueHours { get; set; }
    public decimal? NextDueKilometers { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? UpdatedByUserId { get; set; }
    public List<PreventivePlanScopeEntity> Scopes { get; set; } = [];
}

public sealed class PreventivePlanScopeEntity : PostgreSqlEntity
{
    public Guid PreventivePlanId { get; set; }
    public PreventivePlanEntity PreventivePlan { get; set; } = null!;
    public Guid? AssetId { get; set; }
    public AssetEntity? Asset { get; set; }
    public Guid? EquipmentFamilyId { get; set; }
    public EquipmentFamilyEntity? EquipmentFamily { get; set; }
    public Guid? AssetTypeId { get; set; }
    public AssetTypeEntity? AssetType { get; set; }
    public Guid? OperationalUnitId { get; set; }
    public OperationalUnitEntity? OperationalUnit { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
}

public sealed class PreventiveEvaluationEntity : PostgreSqlEntity
{
    public Guid PreventivePlanId { get; set; }
    public PreventivePlanEntity PreventivePlan { get; set; } = null!;
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public int Status { get; set; }
    public DateTimeOffset EvaluatedAtUtc { get; set; }
    public decimal? CurrentHours { get; set; }
    public decimal? CurrentKilometers { get; set; }
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public string EvaluatedByUserId { get; set; } = string.Empty;
}

public sealed class PreventiveHistoryEntity : PostgreSqlEntity
{
    public Guid PreventivePlanId { get; set; }
    public PreventivePlanEntity PreventivePlan { get; set; } = null!;
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public int PreviousStatus { get; set; }
    public int NewStatus { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
}

public sealed class WorkshopEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid FaenaId { get; set; }
    public FaenaEntity Faena { get; set; } = null!;
    public decimal DailyLaborCapacity { get; set; }
    public int EquipmentCapacity { get; set; }
    public string Schedule { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? UpdatedByUserId { get; set; }
}

public sealed class WorkOrderScheduleEntity : PostgreSqlEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity WorkOrder { get; set; } = null!;
    public Guid WorkshopId { get; set; }
    public WorkshopEntity Workshop { get; set; } = null!;
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public decimal EstimatedLaborHours { get; set; }
    public string? TechnicianUserId { get; set; }
    public int Status { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? UpdatedByUserId { get; set; }
}

public sealed class ScheduleDependencyEntity : PostgreSqlEntity
{
    public Guid PredecessorScheduleId { get; set; }
    public WorkOrderScheduleEntity PredecessorSchedule { get; set; } = null!;
    public Guid SuccessorScheduleId { get; set; }
    public WorkOrderScheduleEntity SuccessorSchedule { get; set; } = null!;
    public string Type { get; set; } = "FinishToStart";
    public string? Reason { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
}

public sealed class ScheduleAlertEntity : PostgreSqlEntity
{
    public int Type { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? WorkshopId { get; set; }
    public WorkshopEntity? Workshop { get; set; }
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public Guid? FaenaId { get; set; }
    public FaenaEntity? Faena { get; set; }
    public bool IsResolved { get; set; }
    public DateTimeOffset RaisedAtUtc { get; set; }
}

public sealed class SupplierEntity : PostgreSqlEntity
{
    public string TaxId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int ExpectedLeadTimeDays { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

public sealed class ProcurementRequestEntity : PostgreSqlEntity
{
    public string RequestNumber { get; set; } = string.Empty;
    public int Status { get; set; }
    public Guid? MaterialRequestId { get; set; }
    public MaterialRequestEntity? MaterialRequest { get; set; }
    public Guid? FaenaId { get; set; }
    public FaenaEntity? Faena { get; set; }
    public Guid? WarehouseId { get; set; }
    public WarehouseEntity? Warehouse { get; set; }
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public Guid? AssetId { get; set; }
    public AssetEntity? Asset { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset TechnicalRequestedAtUtc { get; set; }
    public DateTimeOffset? MaintenanceApprovedAtUtc { get; set; }
    public DateTimeOffset SentToProcurementAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? UpdatedByUserId { get; set; }
    public List<ProcurementRequestLineEntity> Lines { get; set; } = [];
    public List<PurchaseOrderEntity> PurchaseOrders { get; set; } = [];
    public List<ProcurementReceiptEntity> Receipts { get; set; } = [];
}

public sealed class ProcurementRequestLineEntity : PostgreSqlEntity
{
    public Guid ProcurementRequestId { get; set; }
    public ProcurementRequestEntity ProcurementRequest { get; set; } = null!;
    public Guid? SparePartId { get; set; }
    public SparePartEntity? SparePart { get; set; }
    public string? ExternalRequestNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public string Currency { get; set; } = "CLP";
    public string? SupportingDocumentUrl { get; set; }
    public string? Notes { get; set; }
}

public sealed class PurchaseOrderEntity : PostgreSqlEntity
{
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public Guid ProcurementRequestId { get; set; }
    public ProcurementRequestEntity ProcurementRequest { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public SupplierEntity Supplier { get; set; } = null!;
    public DateTimeOffset OrderedAtUtc { get; set; }
    public DateTimeOffset PromisedAtUtc { get; set; }
    public decimal? Cost { get; set; }
    public string Currency { get; set; } = "CLP";
    public string? DocumentUrl { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<PurchaseOrderLineEntity> Lines { get; set; } = [];
}

public sealed class PurchaseOrderLineEntity : PostgreSqlEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrderEntity PurchaseOrder { get; set; } = null!;
    public Guid ProcurementRequestLineId { get; set; }
    public ProcurementRequestLineEntity ProcurementRequestLine { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
}

public sealed class ProcurementReceiptEntity : PostgreSqlEntity
{
    public Guid ProcurementRequestId { get; set; }
    public ProcurementRequestEntity ProcurementRequest { get; set; } = null!;
    public Guid? PurchaseOrderId { get; set; }
    public PurchaseOrderEntity? PurchaseOrder { get; set; }
    public Guid WarehouseId { get; set; }
    public WarehouseEntity Warehouse { get; set; } = null!;
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public bool DirectDispatchToWorkOrder { get; set; }
    public Guid? ReceptionMovementId { get; set; }
    public StockMovementEntity? ReceptionMovement { get; set; }
    public Guid? DeliveryMovementId { get; set; }
    public StockMovementEntity? DeliveryMovement { get; set; }
    public decimal? ActualCost { get; set; }
    public string? ReceptionDocumentUrl { get; set; }
    public string? DeliveryDocumentUrl { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<ProcurementReceiptLineEntity> Lines { get; set; } = [];
}

public sealed class ProcurementReceiptLineEntity : PostgreSqlEntity
{
    public Guid ProcurementReceiptId { get; set; }
    public ProcurementReceiptEntity ProcurementReceipt { get; set; } = null!;
    public Guid ProcurementRequestLineId { get; set; }
    public ProcurementRequestLineEntity ProcurementRequestLine { get; set; } = null!;
    public decimal ReceivedQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
}

public sealed class ImportEntity : PostgreSqlEntity
{
    public string EntityName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public Guid? FileId { get; set; }
    public FileMetadataEntity? File { get; set; }
    public bool SimulateOnly { get; set; }
    public int Status { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; set; }
    public DateTimeOffset? AppliedAtUtc { get; set; }
    public string? AppliedByUserId { get; set; }
    public DateTimeOffset? RejectedAtUtc { get; set; }
    public string? RejectedByUserId { get; set; }
    public string? RejectReason { get; set; }
    public List<ImportRowEntity> Rows { get; set; } = [];
    public List<ImportErrorEntity> Errors { get; set; } = [];
    public List<ImportEventEntity> Events { get; set; } = [];
}

public sealed class ImportRowEntity : PostgreSqlEntity
{
    public Guid ImportId { get; set; }
    public ImportEntity Import { get; set; } = null!;
    public int RowNumber { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string InputSnapshot { get; set; } = "{}";
}

public sealed class ImportErrorEntity : PostgreSqlEntity
{
    public Guid ImportId { get; set; }
    public ImportEntity Import { get; set; } = null!;
    public int RowNumber { get; set; }
    public string? ColumnName { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ImportEventEntity : PostgreSqlEntity
{
    public Guid ImportId { get; set; }
    public ImportEntity Import { get; set; } = null!;
    public int Status { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? Detail { get; set; }
}
