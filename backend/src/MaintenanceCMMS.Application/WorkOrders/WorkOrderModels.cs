namespace MaintenanceCMMS.Application.WorkOrders;

public enum WorkOrderLifecycleStatus
{
    OTCreada = 0,
    EnPlanificacion = 1,
    Programada = 2,
    PendienteRepuestos = 3,
    PendienteDocumentacion = 4,
    EnEjecucion = 5,
    Pausada = 6,
    FinalizadaTecnico = 7,
    EnRevisionSupervisor = 8,
    CerradaTecnicamente = 9,
    ValidadaPlanificacion = 10,
    Anulada = 11
}

public enum WorkOrderSparePartStatus
{
    Solicitado = 0,
    Reservado = 1,
    Entregado = 2,
    Utilizado = 3,
    Devuelto = 4,
    Cancelado = 5
}

public sealed record WorkOrderQuery(
    WorkOrderLifecycleStatus? Status = null,
    string? FaenaCodigo = null,
    string? TechnicianId = null,
    string? ActivoCodigo = null,
    bool IncludeClosed = false);

public sealed record CreateWorkOrderRequest(
    string ActivoCodigo,
    string Descripcion,
    string TipoMantenimiento,
    string? FaenaCodigo = null,
    string? AvisoId = null,
    string? Sistema = null,
    string? Subsistema = null,
    string? Componente = null,
    string Prioridad = "Media",
    string Criticidad = "Media",
    DateTimeOffset? FechaProgramada = null,
    DateTimeOffset? FechaInicioProgramada = null,
    DateTimeOffset? FechaFinProgramada = null,
    bool RequiereFirma = false);

public sealed record CreatePreventiveWorkOrderRequest(
    string ActivoCodigo,
    string Descripcion,
    string? PlanPreventivoCodigo = null,
    string? FaenaCodigo = null,
    string? Sistema = null,
    string? Subsistema = null,
    string? Componente = null,
    DateTimeOffset? FechaProgramada = null,
    DateTimeOffset? FechaInicioProgramada = null,
    DateTimeOffset? FechaFinProgramada = null,
    bool RequiereFirma = false);

public sealed record CreateWorkOrderTaskRequest(
    string Descripcion,
    string? CodigoTarea = null,
    DateTimeOffset? FechaInicioProgramada = null,
    DateTimeOffset? FechaFinProgramada = null,
    bool RequiereEvidencia = false,
    bool RequiereHH = true,
    bool ChecklistObligatorio = false,
    string? Observaciones = null);

public sealed record AssignTaskTechnicianRequest(
    string TecnicoUserId,
    string? TecnicoNombre = null);

public sealed record RegisterLaborRequest(
    string TecnicoUserId,
    decimal Horas,
    string Descripcion,
    DateTimeOffset? FechaTrabajo = null);

public sealed record RegisterEvidenceRequest(
    string Nombre,
    string? CodigoTarea = null,
    string? ArchivoKey = null,
    string? SharePointUrl = null,
    bool CubreEvidenciaObligatoria = true,
    string? Observaciones = null);

public sealed record AddWorkOrderSparePartRequest(
    string CodigoTarea,
    string RepuestoCodigo,
    decimal Cantidad,
    string Unidad,
    string? BodegaCodigo = null,
    WorkOrderSparePartStatus Estado = WorkOrderSparePartStatus.Solicitado,
    string? Observaciones = null);

public sealed record UpdateWorkOrderSparePartUsageRequest(
    WorkOrderSparePartStatus Estado,
    string Reason,
    decimal? CantidadUtilizada = null,
    decimal? CantidadDevuelta = null);

public sealed record AddWorkOrderChecklistItemRequest(
    string CodigoTarea,
    string Item,
    bool Obligatorio = true,
    bool Completado = false);

public sealed record UpdateChecklistItemRequest(
    bool Completado,
    string Reason);

public sealed record RegisterWorkOrderSignatureRequest(
    string SignatureFileKey,
    string? UsuarioId = null,
    string? Comentario = null);

public sealed record ScheduleWorkOrderRequest(
    DateTimeOffset FechaInicioProgramada,
    string Reason,
    DateTimeOffset? FechaFinProgramada = null);

public sealed record WorkOrderActionRequest(string Reason);

public sealed record WorkOrderSummaryResponse(
    string NumeroOT,
    WorkOrderLifecycleStatus Estado,
    string ActivoCodigo,
    string? ActivoNombre,
    string FaenaCodigo,
    string TipoMantenimiento,
    string Descripcion,
    string? AvisoId,
    string? Sistema,
    string? Subsistema,
    string? Componente,
    string Prioridad,
    string Criticidad,
    DateTimeOffset? FechaProgramada,
    DateTimeOffset? FechaInicioProgramada,
    DateTimeOffset? FechaFinProgramada,
    bool EsPreventivaAutomatica,
    bool RequiereFirma,
    int TareasTotal,
    int TecnicosTotal,
    decimal HorasRegistradas,
    int BloqueosCierre);

public sealed record WorkOrderDetailResponse(
    WorkOrderSummaryResponse Summary,
    IReadOnlyCollection<WorkOrderTaskResponse> Tasks,
    IReadOnlyCollection<WorkOrderTaskTechnicianResponse> Technicians,
    IReadOnlyCollection<WorkOrderLaborResponse> Labor,
    IReadOnlyCollection<WorkOrderEvidenceResponse> Evidences,
    IReadOnlyCollection<WorkOrderSparePartResponse> SpareParts,
    IReadOnlyCollection<WorkOrderChecklistItemResponse> Checklist,
    IReadOnlyCollection<WorkOrderSignatureResponse> Signatures,
    IReadOnlyCollection<WorkOrderStatusHistoryResponse> History,
    IReadOnlyCollection<WorkOrderClosureBlocker> ClosureBlockers);

public sealed record WorkOrderTaskResponse(
    string NumeroOT,
    string CodigoTarea,
    string Descripcion,
    DateTimeOffset? FechaInicioProgramada,
    DateTimeOffset? FechaFinProgramada,
    bool RequiereEvidencia,
    bool RequiereHH,
    bool ChecklistObligatorio,
    string? Observaciones);

public sealed record WorkOrderTaskTechnicianResponse(
    string AsignacionId,
    string NumeroOT,
    string CodigoTarea,
    string TecnicoUserId,
    string? TecnicoNombre,
    DateTimeOffset AsignadoEnUtc,
    string AsignadoPor);

public sealed record WorkOrderLaborResponse(
    string HHId,
    string NumeroOT,
    string CodigoTarea,
    string TecnicoUserId,
    decimal Horas,
    string Descripcion,
    DateTimeOffset FechaTrabajo,
    string RegistradoPor);

public sealed record WorkOrderEvidenceResponse(
    string EvidenciaId,
    string NumeroOT,
    string? CodigoTarea,
    string Nombre,
    string? ArchivoKey,
    string? SharePointUrl,
    bool CubreEvidenciaObligatoria,
    DateTimeOffset CreadoEnUtc,
    string CreadoPor,
    string? Observaciones);

public sealed record WorkOrderSparePartResponse(
    string ItemId,
    string NumeroOT,
    string CodigoTarea,
    string RepuestoCodigo,
    decimal Cantidad,
    string Unidad,
    string? BodegaCodigo,
    WorkOrderSparePartStatus Estado,
    decimal CantidadUtilizada,
    decimal CantidadDevuelta,
    string? Observaciones);

public sealed record WorkOrderChecklistItemResponse(
    string ItemId,
    string NumeroOT,
    string CodigoTarea,
    string Item,
    bool Obligatorio,
    bool Completado,
    DateTimeOffset? CompletadoEnUtc,
    string? CompletadoPor);

public sealed record WorkOrderSignatureResponse(
    string FirmaId,
    string NumeroOT,
    string UsuarioId,
    string SignatureFileKey,
    DateTimeOffset FirmadoEnUtc,
    string? Comentario);

public sealed record WorkOrderStatusHistoryResponse(
    string HistorialId,
    string NumeroOT,
    WorkOrderLifecycleStatus EstadoAnterior,
    WorkOrderLifecycleStatus EstadoNuevo,
    DateTimeOffset FechaUtc,
    string UsuarioId,
    string Motivo);

public sealed record WorkOrderClosureBlocker(
    string Code,
    string Message);
