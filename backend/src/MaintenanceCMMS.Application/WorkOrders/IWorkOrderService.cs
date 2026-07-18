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

    Task<WorkOrderSupervisorResponse?> AssignSupervisorAsync(string numeroOt, AssignWorkOrderSupervisorRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderDetailResponse?> SendToSupervisorAsync(string numeroOt, WorkOrderActionRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkOrderSummaryResponse>> ListSupervisorAssignedAsync(UserAccessContext user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkOrderTechnicianResponse>?> ListTechniciansAsync(string numeroOt, UserAccessContext user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkOrderTechnicianResponse>?> AssignTechniciansAsync(string numeroOt, AssignWorkOrderTechniciansRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderTechnicianResponse?> UnassignTechnicianAsync(string numeroOt, Guid usuarioId, UnassignWorkOrderTechnicianRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MyAssignedWorkOrderResponse>> ListMyAssignedAsync(UserAccessContext user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkOrderUserLookupResponse>> ListSupervisorsAsync(string? faenaCodigo, UserAccessContext user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkOrderUserLookupResponse>> ListTechnicianLookupsAsync(string? faenaCodigo, UserAccessContext user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkOrderTaskResponse>?> ListTasksAsync(string numeroOt, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderTaskResponse?> UpdateTaskAsync(string numeroOt, string codigoTarea, UpdateWorkOrderTaskRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderTaskResponse?> CancelTaskAsync(string numeroOt, string codigoTarea, WorkOrderTaskActionRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderTaskResponse?> StartTaskAsync(string numeroOt, string codigoTarea, WorkOrderTaskActionRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderTaskResponse?> CompleteTaskAsync(string numeroOt, string codigoTarea, WorkOrderTaskActionRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderTaskResponse?> ObserveTaskAsync(string numeroOt, string codigoTarea, ObserveWorkOrderTaskRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderTaskResponse?> ApproveTaskAsync(string numeroOt, string codigoTarea, WorkOrderTaskActionRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkOrderLaborResponse>?> ListTaskLaborAsync(string numeroOt, string codigoTarea, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderLaborResponse?> RegisterOwnLaborAsync(string numeroOt, string codigoTarea, RegisterOwnLaborRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderLaborResponse?> UpdateOwnLaborAsync(string numeroOt, Guid registroId, UpdateOwnLaborRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderLaborResponse?> ApproveLaborAsync(string numeroOt, Guid registroId, WorkOrderTaskActionRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderLaborResponse?> VoidLaborAsync(string numeroOt, Guid registroId, VoidWorkOrderLaborRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkOrderEvidenceResponse>?> ListTaskEvidencesAsync(string numeroOt, string codigoTarea, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderEvidenceResponse?> RegisterUploadedEvidenceAsync(string numeroOt, string codigoTarea, UploadWorkOrderEvidenceRequest request, Guid fileId, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderEvidenceResponse?> VoidEvidenceAsync(string numeroOt, Guid evidenciaId, VoidWorkOrderEvidenceRequest request, UserAccessContext user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkOrderSignatureResponse>?> ListSignaturesAsync(string numeroOt, UserAccessContext user, CancellationToken cancellationToken);
    Task<WorkOrderSignatureResponse?> RegisterOwnSignatureAsync(string numeroOt, RegisterOwnWorkOrderSignatureRequest request, Guid fileId, UserAccessContext user, CancellationToken cancellationToken);

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
