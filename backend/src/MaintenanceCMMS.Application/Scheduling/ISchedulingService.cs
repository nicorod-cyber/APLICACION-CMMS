using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Scheduling;

public interface ISchedulingService
{
    Task<ScheduleBoardResponse> GetBoardAsync(
        ScheduleBoardQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkshopResponse>> ListWorkshopsAsync(
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkshopResponse> UpsertWorkshopAsync(
        UpsertWorkshopRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<ScheduleWorkOrderResponse> ScheduleWorkOrderAsync(
        string numeroOt,
        ScheduleWorkOrderPlanningRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<ScheduleDependencyResponse> AddDependencyAsync(
        AddScheduleDependencyRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ScheduleAlertResponse>> ListAlertsAsync(
        ScheduleBoardQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
