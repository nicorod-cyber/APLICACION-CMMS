using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Governance;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Infrastructure.Governance;

public sealed class DataGovernanceService : IDataGovernanceService
{
    private static readonly HashSet<string> NoPhysicalDeleteModules = new(StringComparer.OrdinalIgnoreCase)
    {
        AuditModules.Documents,
        AuditModules.Stock,
        AuditModules.WorkOrders
    };

    private readonly IAuditService _auditService;

    public DataGovernanceService(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public Task<string> AuditAssetCreationAsync(
        AssetCreationAuditRequest request,
        CancellationToken cancellationToken)
    {
        return _auditService.RecordAsync(new AuditEventRequest(
            request.Actor.UserId,
            "asset.created",
            AuditModules.Assets,
            "Asset",
            request.AssetId,
            NewValue: request.NewValue,
            FaenaCodigo: request.Actor.FaenaCodigo,
            Severity: AuditSeverity.Medium,
            Detail: $"Activo {request.AssetCode} creado"), cancellationToken);
    }

    public Task<string> AuditStockAdjustmentAsync(
        StockAdjustmentAuditRequest request,
        CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(request.Reason, nameof(request.Reason));

        return _auditService.RecordAsync(new AuditEventRequest(
            request.Actor.UserId,
            "stock.adjusted",
            AuditModules.Stock,
            "StockItem",
            request.StockItemId,
            PreviousValue: request.PreviousValue,
            NewValue: request.NewValue,
            FaenaCodigo: request.Actor.FaenaCodigo,
            Severity: AuditSeverity.Critical,
            Reason: request.Reason,
            Detail: "Ajuste de stock con motivo obligatorio"), cancellationToken);
    }

    public Task<string> AuditValidatedDocumentChangeAsync(
        ValidatedDocumentChangeRequest request,
        CancellationToken cancellationToken)
    {
        EnsureValidatedFieldCanChange(request);
        EnsureCorrectionHasReason(new GovernanceCorrectionRequest(
            AuditModules.Documents,
            "AssetDocument",
            request.DocumentId,
            request.Reason,
            AuditSeverity.Critical));
        EnsureCriticalChangeApproved(new CriticalChangeApprovalRequest(
            AuditModules.Documents,
            "AssetDocument",
            request.DocumentId,
            request.HasApproval,
            request.ApprovalUserId,
            request.Reason));

        return _auditService.RecordAsync(new AuditEventRequest(
            request.Actor.UserId,
            "document.validated_field_changed",
            AuditModules.Documents,
            "AssetDocument",
            request.DocumentId,
            PreviousValue: request.PreviousValue,
            NewValue: request.NewValue,
            FaenaCodigo: request.Actor.FaenaCodigo,
            Severity: AuditSeverity.Critical,
            Reason: request.Reason,
            Detail: $"Campo validado modificado: {request.FieldName}"), cancellationToken);
    }

    public void EnsurePhysicalDeleteAllowed(PhysicalDeleteRequest request)
    {
        if (NoPhysicalDeleteModules.Contains(request.Module))
        {
            throw new DomainException($"{request.Module}/{request.EntityName} no permite eliminacion fisica. Use anulacion o reemplazo.");
        }
    }

    public void EnsureCorrectionHasReason(GovernanceCorrectionRequest request)
    {
        DomainGuard.AgainstEmpty(request.Reason ?? string.Empty, "reason");
    }

    public void EnsureCriticalChangeApproved(CriticalChangeApprovalRequest request)
    {
        if (!request.IsApproved)
        {
            throw new DomainException($"{request.Module}/{request.EntityName} requiere aprobacion antes de aplicar cambios criticos.");
        }

        DomainGuard.AgainstEmpty(request.ApprovalUserId ?? string.Empty, "approvalUserId");
    }

    public void EnsureValidatedFieldCanChange(ValidatedDocumentChangeRequest request)
    {
        var fieldIsExpiryDate = request.FieldName.Equals("FechaVencimiento", StringComparison.OrdinalIgnoreCase) ||
                                request.FieldName.Equals("ExpiresOn", StringComparison.OrdinalIgnoreCase);

        if (request.State is not (DataGovernanceState.Validated or DataGovernanceState.Locked))
        {
            return;
        }

        if (fieldIsExpiryDate && request.HasValidatedExpiryPermission)
        {
            return;
        }

        throw new DomainException("Los campos validados quedan bloqueados y solo pueden corregirse con permiso, motivo y auditoria.");
    }
}
