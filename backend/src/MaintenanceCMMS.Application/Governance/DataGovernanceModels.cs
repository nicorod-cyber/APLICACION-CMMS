using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Application.Governance;

public sealed record GovernanceActor(
    string UserId,
    string? FaenaCodigo = null);

public sealed record AssetCreationAuditRequest(
    GovernanceActor Actor,
    string AssetId,
    string AssetCode,
    string NewValue);

public sealed record StockAdjustmentAuditRequest(
    GovernanceActor Actor,
    string StockItemId,
    string PreviousValue,
    string NewValue,
    string Reason);

public sealed record ValidatedDocumentChangeRequest(
    GovernanceActor Actor,
    string DocumentId,
    string FieldName,
    string? PreviousValue,
    string? NewValue,
    DataGovernanceState State,
    bool HasValidatedExpiryPermission,
    string? Reason,
    bool HasApproval = false,
    string? ApprovalUserId = null);

public sealed record CriticalChangeApprovalRequest(
    string Module,
    string EntityName,
    string EntityId,
    bool IsApproved,
    string? ApprovalUserId,
    string? Reason);

public sealed record PhysicalDeleteRequest(
    string Module,
    string EntityName,
    string EntityId);

public sealed record GovernanceCorrectionRequest(
    string Module,
    string EntityName,
    string EntityId,
    string? Reason,
    AuditSeverity Severity = AuditSeverity.High);
