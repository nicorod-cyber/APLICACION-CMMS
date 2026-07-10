namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;

public sealed class WorkCatalogEntity : PostgreSqlEntity
{
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class WorkNotificationEntity : PostgreSqlEntity
{
    public string NotificationNumber { get; set; } = string.Empty;
    public Guid StatusId { get; set; }
    public WorkCatalogEntity Status { get; set; } = null!;
    public Guid TypeId { get; set; }
    public WorkCatalogEntity Type { get; set; } = null!;
    public Guid FaenaId { get; set; }
    public FaenaEntity Faena { get; set; } = null!;
    public Guid? AssetId { get; set; }
    public AssetEntity? Asset { get; set; }
    public string? System { get; set; }
    public string? Subsystem { get; set; }
    public string? Component { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid PriorityId { get; set; }
    public WorkCatalogEntity Priority { get; set; } = null!;
    public Guid CriticalityId { get; set; }
    public WorkCatalogEntity Criticality { get; set; } = null!;
    public string RequesterUserId { get; set; } = string.Empty;
    public string? InitialEvidenceReference { get; set; }
    public DateTimeOffset DetectedAtUtc { get; set; }
    public DateTimeOffset CreatedByUserAtUtc { get; set; }
    public Guid FailureClassificationId { get; set; }
    public WorkCatalogEntity FailureClassification { get; set; } = null!;
    public string? EvaluatedByUserId { get; set; }
    public DateTimeOffset? EvaluatedAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public string? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAtUtc { get; set; }
    public string? RejectReason { get; set; }
    public string? AnnulledByUserId { get; set; }
    public DateTimeOffset? AnnulledAtUtc { get; set; }
    public string? AnnulReason { get; set; }
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public string? ConvertedByUserId { get; set; }
    public DateTimeOffset? ConvertedAtUtc { get; set; }
    public string? Observations { get; set; }
}

public sealed class WorkOrderEntity : PostgreSqlEntity
{
    public string WorkOrderNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public AssetEntity Asset { get; set; } = null!;
    public Guid FaenaId { get; set; }
    public FaenaEntity Faena { get; set; } = null!;
    public Guid StatusId { get; set; }
    public WorkCatalogEntity Status { get; set; } = null!;
    public Guid MaintenanceTypeId { get; set; }
    public WorkCatalogEntity MaintenanceType { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public Guid? NotificationId { get; set; }
    public WorkNotificationEntity? Notification { get; set; }
    public string? System { get; set; }
    public string? Subsystem { get; set; }
    public string? Component { get; set; }
    public Guid? PriorityId { get; set; }
    public WorkCatalogEntity? Priority { get; set; }
    public Guid? CriticalityId { get; set; }
    public WorkCatalogEntity? Criticality { get; set; }
    public Guid? FailureClassificationId { get; set; }
    public WorkCatalogEntity? FailureClassification { get; set; }
    public string? PreventivePlanCode { get; set; }
    public bool IsAutomaticPreventive { get; set; }
    public bool RequiresSignature { get; set; }
    public DateTimeOffset? ScheduledAtUtc { get; set; }
    public DateTimeOffset? ScheduledStartUtc { get; set; }
    public DateTimeOffset? ScheduledEndUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedByUserAtUtc { get; set; }
    public DateTimeOffset? ActualStartUtc { get; set; }
    public DateTimeOffset? TechnicianFinishedAtUtc { get; set; }
    public string? FinishedByUserId { get; set; }
    public DateTimeOffset? SupervisorClosedAtUtc { get; set; }
    public string? ClosedByUserId { get; set; }
    public DateTimeOffset? PlanningValidatedAtUtc { get; set; }
    public string? ValidatedByUserId { get; set; }
    public string? AnnulledByUserId { get; set; }
    public DateTimeOffset? AnnulledAtUtc { get; set; }
    public string? AnnulReason { get; set; }
    public string? UpdatedByUserId { get; set; }
    public DateTimeOffset? UpdatedByUserAtUtc { get; set; }
    public List<WorkOrderTaskEntity> Tasks { get; set; } = [];
    public List<WorkOrderTaskTechnicianEntity> Technicians { get; set; } = [];
    public List<WorkOrderLaborEntity> Labor { get; set; } = [];
    public List<WorkOrderEvidenceEntity> Evidences { get; set; } = [];
    public List<WorkOrderSparePartEntity> SpareParts { get; set; } = [];
    public List<WorkOrderChecklistEntity> Checklist { get; set; } = [];
    public List<WorkOrderSignatureEntity> Signatures { get; set; } = [];
    public List<WorkOrderStatusHistoryEntity> History { get; set; } = [];
}

public sealed class WorkOrderTaskEntity : PostgreSqlEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity WorkOrder { get; set; } = null!;
    public string TaskCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset? ScheduledStartUtc { get; set; }
    public DateTimeOffset? ScheduledEndUtc { get; set; }
    public bool RequiresEvidence { get; set; }
    public bool RequiresLabor { get; set; } = true;
    public bool ChecklistMandatory { get; set; }
    public string? Observations { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WorkOrderTaskTechnicianEntity : PostgreSqlEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity WorkOrder { get; set; } = null!;
    public Guid TaskId { get; set; }
    public WorkOrderTaskEntity Task { get; set; } = null!;
    public string TechnicianUserId { get; set; } = string.Empty;
    public string? TechnicianDisplayName { get; set; }
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string AssignedByUserId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? UnassignedAtUtc { get; set; }
    public string? UnassignedByUserId { get; set; }
    public string? UnassignedReason { get; set; }
}

public sealed class WorkOrderLaborEntity : PostgreSqlEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity WorkOrder { get; set; } = null!;
    public Guid TaskId { get; set; }
    public WorkOrderTaskEntity Task { get; set; } = null!;
    public string TechnicianUserId { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset WorkDateUtc { get; set; }
    public DateTimeOffset? StartTimeUtc { get; set; }
    public DateTimeOffset? EndTimeUtc { get; set; }
    public string RegisteredByUserId { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public bool SupervisorValidated { get; set; }
    public string? ValidatedByUserId { get; set; }
    public DateTimeOffset? ValidatedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WorkOrderEvidenceEntity : PostgreSqlEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity WorkOrder { get; set; } = null!;
    public Guid? TaskId { get; set; }
    public WorkOrderTaskEntity? Task { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? FileId { get; set; }
    public FileMetadataEntity? File { get; set; }
    public Guid EvidenceTypeId { get; set; }
    public WorkCatalogEntity EvidenceType { get; set; } = null!;
    public bool IsPhoto { get; set; }
    public bool IsMandatory { get; set; }
    public bool CoversMandatoryEvidence { get; set; } = true;
    public string? StorageProvider { get; set; }
    public string? ExternalUri { get; set; }
    public string? ExternalKey { get; set; }
    public string? LocalPath { get; set; }
    public string? OfflineId { get; set; }
    public string? SyncStatus { get; set; }
    public string? Observations { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedByUserAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
}

public sealed class WorkOrderSparePartEntity : PostgreSqlEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity WorkOrder { get; set; } = null!;
    public Guid TaskId { get; set; }
    public WorkOrderTaskEntity Task { get; set; } = null!;
    public string SparePartCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? WarehouseCode { get; set; }
    public Guid StatusId { get; set; }
    public WorkCatalogEntity Status { get; set; } = null!;
    public decimal UsedQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public string? Observations { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ChecklistTemplateEntity : PostgreSqlEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? WorkOrderTypeCode { get; set; }
    public string? FamilyCode { get; set; }
    public string? PreventivePlanCode { get; set; }
    public string? TaskCode { get; set; }
    public string? AssetCode { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ChecklistTemplateItemEntity> Items { get; set; } = [];
}

public sealed class ChecklistTemplateItemEntity : PostgreSqlEntity
{
    public Guid TemplateId { get; set; }
    public ChecklistTemplateEntity Template { get; set; } = null!;
    public int SortOrder { get; set; }
    public string ItemText { get; set; } = string.Empty;
    public bool Mandatory { get; set; } = true;
    public Guid ResponseTypeId { get; set; }
    public WorkCatalogEntity ResponseType { get; set; } = null!;
    public bool RequiresPhoto { get; set; }
    public bool RequiresFile { get; set; }
    public bool RequiresSignature { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WorkOrderChecklistEntity : PostgreSqlEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity WorkOrder { get; set; } = null!;
    public Guid TaskId { get; set; }
    public WorkOrderTaskEntity Task { get; set; } = null!;
    public Guid? TemplateId { get; set; }
    public ChecklistTemplateEntity? Template { get; set; }
    public Guid? TemplateItemId { get; set; }
    public ChecklistTemplateItemEntity? TemplateItem { get; set; }
    public string ItemText { get; set; } = string.Empty;
    public bool Mandatory { get; set; } = true;
    public bool Completed { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? CompletedByUserId { get; set; }
    public Guid ResponseTypeId { get; set; }
    public WorkCatalogEntity ResponseType { get; set; } = null!;
    public string? Response { get; set; }
    public decimal? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public Guid? EvidenceId { get; set; }
    public WorkOrderEvidenceEntity? Evidence { get; set; }
    public Guid? SignatureId { get; set; }
    public WorkOrderSignatureEntity? Signature { get; set; }
    public bool RequiresPhoto { get; set; }
    public bool RequiresFile { get; set; }
    public bool RequiresSignature { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WorkOrderSignatureEntity : PostgreSqlEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity WorkOrder { get; set; } = null!;
    public Guid? TaskId { get; set; }
    public WorkOrderTaskEntity? Task { get; set; }
    public string Scope { get; set; } = "OT";
    public string SignerUserId { get; set; } = string.Empty;
    public Guid? FileId { get; set; }
    public FileMetadataEntity? File { get; set; }
    public string? SignatureFileKey { get; set; }
    public DateTimeOffset SignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? Comment { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WorkOrderStatusHistoryEntity : PostgreSqlEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity WorkOrder { get; set; } = null!;
    public Guid PreviousStatusId { get; set; }
    public WorkCatalogEntity PreviousStatus { get; set; } = null!;
    public Guid NewStatusId { get; set; }
    public WorkCatalogEntity NewStatus { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class DocumentWorkOrderEntity : PostgreSqlEntity
{
    public Guid DocumentId { get; set; }
    public DocumentEntity Document { get; set; } = null!;
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity WorkOrder { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? AssignedByUserId { get; set; }
    public DateTimeOffset? UnassignedAtUtc { get; set; }
    public string? UnassignedByUserId { get; set; }
    public string? UnassignedReason { get; set; }
}
