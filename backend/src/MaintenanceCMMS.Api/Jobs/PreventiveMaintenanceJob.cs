using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.PreventiveMaintenance;
using Quartz;

namespace MaintenanceCMMS.Api.Jobs;

public sealed class PreventiveMaintenanceJob : IJob
{
    private static readonly UserAccessContext JobUser = new(
        "preventive-engine",
        [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.ManageAlerts, AuthPermissions.FinalValidateWorkOrders],
        []);

    private readonly IPreventiveMaintenanceService _service;
    private readonly ILogger<PreventiveMaintenanceJob> _logger;

    public PreventiveMaintenanceJob(
        IPreventiveMaintenanceService service,
        ILogger<PreventiveMaintenanceJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var result = await _service.RunAutomaticEvaluationAsync(JobUser, context.CancellationToken);
        _logger.LogInformation(
            "Preventive engine evaluated {Evaluated} items and generated {GeneratedWorkOrders} work orders with {AlertsGenerated} alerts.",
            result.Evaluated,
            result.GeneratedWorkOrders,
            result.AlertsGenerated);
    }
}
