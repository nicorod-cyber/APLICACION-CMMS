using MaintenanceCMMS.Application.Documents;
using Quartz;

namespace MaintenanceCMMS.Api.Jobs;

public sealed class DocumentaryWorkOrderJob(IDocumentaryWorkOrderService service, ILogger<DocumentaryWorkOrderJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var result = await service.RunAsync(DateOnly.FromDateTime(DateTime.UtcNow), "documentary-engine", context.CancellationToken);
        logger.LogInformation("Documentary engine evaluated {Assets} assets, created {Orders} work orders and {Requirements} requirements.", result.ActivosEvaluados, result.OrdenesCreadas, result.RequisitosCreados);
    }
}
