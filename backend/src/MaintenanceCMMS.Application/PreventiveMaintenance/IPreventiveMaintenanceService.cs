using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.PreventiveMaintenance;

public interface IPreventiveMaintenanceService
{
    Task<IReadOnlyCollection<PreventivePlanResponse>> ListPlansAsync(
        PreventivePlanQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<PreventivePlanResponse> UpsertPlanAsync(
        UpsertPreventivePlanRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PreventiveReadingResponse>> ListReadingsAsync(
        PreventiveReadingQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<PreventiveReadingResponse> RegisterReadingAsync(
        RegisterPreventiveReadingRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<PreventiveDashboardResponse> EvaluateAsync(
        PreventiveEvaluationQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<PreventiveWorkOrderGenerationResponse> GenerateWorkOrderAsync(
        string planCode,
        GeneratePreventiveWorkOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<PreventivePlanResponse?> ReprogramAsync(
        string planCode,
        ReprogramPreventivePlanRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<PreventiveEngineRunResponse> RunAutomaticEvaluationAsync(
        UserAccessContext user,
        CancellationToken cancellationToken);
}
