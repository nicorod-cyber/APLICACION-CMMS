using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.MaterialRequests;

public interface IMaterialRequestService
{
    Task<IReadOnlyCollection<MaterialRequestResponse>> ListAsync(
        MaterialRequestQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<MaterialRequestResponse?> GetByIdAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<MaterialRequestResponse> CreateAsync(
        CreateMaterialRequestRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<MaterialRequestResponse?> ApproveMaintenanceAsync(
        string id,
        MaterialRequestReasonRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<MaterialRequestResponse?> RejectAsync(
        string id,
        MaterialRequestReasonRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<MaterialRequestResponse?> ReviewWarehouseAsync(
        string id,
        WarehouseReviewMaterialRequestRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<MaterialRequestResponse?> PrepareAsync(
        string id,
        MaterialRequestReasonRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<MaterialRequestResponse?> DeliverAsync(
        string id,
        DeliverRequestedMaterialRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<MaterialRequestResponse?> ReceiveAsync(
        string id,
        MaterialRequestReasonRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<MaterialRequestResponse?> CloseAsync(
        string id,
        MaterialRequestReasonRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<MaterialRequestResponse?> ConvertToSparePartAsync(
        string id,
        ConvertMaterialRequestToSparePartRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
