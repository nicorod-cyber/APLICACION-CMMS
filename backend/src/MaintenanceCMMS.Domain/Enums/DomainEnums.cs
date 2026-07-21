namespace MaintenanceCMMS.Domain.Enums;

public enum AssetStatus
{
    Draft = 0,
    Active = 1,
    InMaintenance = 2,
    Unavailable = 3,
    Retired = 4
}

public enum WorkOrderStatus
{
    Draft = 0,
    Planned = 1,
    Assigned = 2,
    InProgress = 3,
    PendingEvidence = 4,
    PendingLabor = 5,
    Closed = 6,
    Cancelled = 7
}

public enum WorkNoticeStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    ConvertedToWorkOrder = 3,
    Rejected = 4,
    Cancelled = 5
}

public enum SparePartRequestStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Ordered = 3,
    Received = 4,
    Rejected = 5,
    Cancelled = 6
}

public enum DocumentStatus
{
    Draft = 0,
    PendingValidation = 1,
    Validated = 2,
    Expired = 3,
    Rejected = 4,
    Archived = 5
}

public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum StockMovementType
{
    Reception = 0,
    MaintenanceConsumption = 1,
    Reservation = 2,
    ReservationRelease = 3,
    TransferOut = 4,
    TransferIn = 5,
    Adjustment = 6,
    CountCorrection = 7,
    ReturnFromWorkOrder = 8,
    PositiveAdjustment = 9,
    NegativeAdjustment = 10,
    MaterialWriteOff = 11,
    InTransit = 12,
    TransferReception = 13
}

public enum UserRoleType
{
    Administrator = 0,
    Planner = 1,
    Supervisor = 2,
    Technician = 3,
    Warehouse = 4,
    Procurement = 5,
    CostController = 6,
    Viewer = 7
}

public enum DataProviderType
{
    Excel = 0,
    SqlServer = 1,
    PostgreSql = 2
}

public enum ImportStatus
{
    Draft = 0,
    Validating = 1,
    PendingApproval = 2,
    Approved = 3,
    Applied = 4,
    Rejected = 5,
    Failed = 6
}

public enum SyncStatus
{
    Pending = 0,
    Synced = 1,
    Conflict = 2,
    Failed = 3
}

public enum CostType
{
    SparePart = 0,
    Labor = 1,
    ExternalService = 2,
    PaymentStatement = 3
}

public enum MaintenanceType
{
    Corrective = 0,
    Preventive = 1,
    Predictive = 2,
    Inspection = 3,
    Documentary = 4
}

public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Approved = 2,
    Rejected = 3,
    Deleted = 4,
    Closed = 5
}

public enum DataGovernanceState
{
    Draft = 0,
    PendingValidation = 1,
    Validated = 2,
    Rejected = 3,
    Locked = 4,
    Replaced = 5,
    Annulled = 6
}
