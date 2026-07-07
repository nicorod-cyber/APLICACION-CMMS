using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Procurement;

public interface IProcurementService
{
    Task<IReadOnlyCollection<SupplierResponse>> ListSuppliersAsync(
        SupplierQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<SupplierResponse?> GetSupplierAsync(
        string rut,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<SupplierResponse> CreateSupplierAsync(
        UpsertSupplierRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<SupplierResponse?> UpdateSupplierAsync(
        string rut,
        UpsertSupplierRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProcurementRequestResponse>> ListRequestsAsync(
        ProcurementRequestQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<ProcurementRequestResponse?> GetRequestAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<ProcurementRequestResponse> CreateRequestAsync(
        CreateProcurementRequestRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<ProcurementRequestResponse?> LinkPurchaseOrderAsync(
        string id,
        LinkPurchaseOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<ProcurementRequestResponse?> RegisterReceptionAsync(
        string id,
        RegisterProcurementReceptionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<ProcurementRequestResponse?> RegisterDeliveryAsync(
        string id,
        DeliverProcurementRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
