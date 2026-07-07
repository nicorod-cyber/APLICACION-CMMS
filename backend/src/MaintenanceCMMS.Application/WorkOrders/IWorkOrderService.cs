using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.WorkOrders;

public interface IWorkOrderService
{
    Task<IReadOnlyCollection<WorkOrderSummaryResponse>> ListAsync(
        WorkOrderQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderDetailResponse?> GetByIdAsync(
        string numeroOt,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderDetailResponse> CreateAsync(
        CreateWorkOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderDetailResponse> CreatePreventiveAsync(
        CreatePreventiveWorkOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderTaskResponse?> AddTaskAsync(
        string numeroOt,
        CreateWorkOrderTaskRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderTaskTechnicianResponse?> AssignTechnicianAsync(
        string numeroOt,
        string codigoTarea,
        AssignTaskTechnicianRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderLaborResponse?> RegisterLaborAsync(
        string numeroOt,
        string codigoTarea,
        RegisterLaborRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderLaborResponse?> ValidateLaborAsync(
        string numeroOt,
        string hhId,
        ValidateLaborRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderEvidenceResponse?> RegisterEvidenceAsync(
        string numeroOt,
        RegisterEvidenceRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderSparePartResponse?> AddSparePartAsync(
        string numeroOt,
        AddWorkOrderSparePartRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderSparePartResponse?> UpdateSparePartUsageAsync(
        string numeroOt,
        string itemId,
        UpdateWorkOrderSparePartUsageRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderChecklistItemResponse?> AddChecklistItemAsync(
        string numeroOt,
        AddWorkOrderChecklistItemRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderChecklistItemResponse?> UpdateChecklistItemAsync(
        string numeroOt,
        string itemId,
        UpdateChecklistItemRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkOrderChecklistItemResponse>> ApplyChecklistTemplateAsync(
        string numeroOt,
        ApplyChecklistTemplateRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderSignatureResponse?> RegisterSignatureAsync(
        string numeroOt,
        RegisterWorkOrderSignatureRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderDetailResponse?> ScheduleAsync(
        string numeroOt,
        ScheduleWorkOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderDetailResponse?> StartAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderDetailResponse?> PauseAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderDetailResponse?> FinishByTechnicianAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderDetailResponse?> CloseTechnicallyAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderDetailResponse?> ValidatePlanningAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);

    Task<WorkOrderDetailResponse?> AnnulAsync(
        string numeroOt,
        WorkOrderActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken);
}
