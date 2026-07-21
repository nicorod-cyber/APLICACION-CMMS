using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Availability;

public interface IAvailabilityService
{
    Task<AvailabilityDashboardResponse> GetDashboardAsync(
        AvailabilityQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AvailabilityContractResponse>> ListContractsAsync(
        AvailabilityContractQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AvailabilityContractResponse> UpsertContractAsync(
        UpsertAvailabilityContractRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AvailabilityContractAssetResponse> AssignAssetAsync(
        AssignContractAssetRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AvailabilityContractAssetResponse> AssignTargetAsync(
        AssignContractTargetRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AvailabilityEventResponse>> ListEventsAsync(
        AvailabilityEventQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<AvailabilityEventResponse> RegisterEventAsync(
        RegisterAvailabilityEventRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
