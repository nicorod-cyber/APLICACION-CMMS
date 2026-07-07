namespace MaintenanceCMMS.Application.Governance;

public interface IDataGovernanceService
{
    Task<string> AuditAssetCreationAsync(AssetCreationAuditRequest request, CancellationToken cancellationToken);

    Task<string> AuditStockAdjustmentAsync(StockAdjustmentAuditRequest request, CancellationToken cancellationToken);

    Task<string> AuditValidatedDocumentChangeAsync(ValidatedDocumentChangeRequest request, CancellationToken cancellationToken);

    void EnsurePhysicalDeleteAllowed(PhysicalDeleteRequest request);

    void EnsureCorrectionHasReason(GovernanceCorrectionRequest request);

    void EnsureCriticalChangeApproved(CriticalChangeApprovalRequest request);

    void EnsureValidatedFieldCanChange(ValidatedDocumentChangeRequest request);
}
