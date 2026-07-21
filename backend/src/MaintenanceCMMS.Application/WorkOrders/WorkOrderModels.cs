using MaintenanceCMMS.Application.MaintenanceTargets;

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
    Anulada = 11,
    EnviadaSupervisor = 12
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

public enum WorkOrderTaskStatus
{
    PendienteAsignacion = 0,
    Asignada = 1,
    EnEjecucion = 2,
    CompletadaTecnico = 3,
    EnRevisionSupervisor = 4,
    Observada = 5,
    AprobadaSupervisor = 6,
    Cancelada = 7
}

public enum WorkOrderTaskOrigin
{
    ManualSupervisor = 0,
    PautaPreventiva = 1,
    HallazgoEjecucion = 2
}

public enum WorkOrderEvidenceType
{
    FotoAntes = 0,
    FotoDurante = 1,
    FotoDespues = 2,
    FotoPrueba = 3,
    Archivo = 4,
    Comentario = 5,
    Otro = 6
}

public enum WorkOrderChecklistResponseType
{
    CumpleNoCumpleNoAplica = 0,
    BuenoRegularMalo = 1,
    SiNo = 2,
    Numerico = 3,
    Texto = 4,
    FotoObligatoria = 5,
    Archivo = 6,
    Firma = 7
}

public sealed record WorkOrderQuery(
    WorkOrderLifecycleStatus? Status = null,
    string? FaenaCodigo = null,
    string? TechnicianId = null,
    string? ActivoCodigo = null,
    bool IncludeClosed = false,
    string? UnidadOperativaCodigo = null,
    MaintenanceTargetType? TipoObjetivo = null,
    string? ObjetivoCodigo = null);

public sealed record CreateWorkOrderRequest(
    string? ActivoCodigo,
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
    bool RequiereFirma = false,
    string? UnidadOperativaCodigo = null,
    IReadOnlyCollection<WorkOrderAssetInput>? ActivosRelacionados = null,
    MaintenanceTargetReference? Objetivo = null);

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

public sealed record RegisterLaborRequest(
    string TecnicoUserId,
    decimal? Horas,
    string Descripcion,
    DateTimeOffset? FechaTrabajo = null,
    DateTimeOffset? HoraInicio = null,
    DateTimeOffset? HoraTermino = null,
    string? Comentario = null);

public sealed record ValidateLaborRequest(
    bool Validado,
    string Reason);

public sealed record RegisterEvidenceRequest(
    string Nombre,
    string? CodigoTarea = null,
    string? ArchivoKey = null,
    string? SharePointUrl = null,
    bool CubreEvidenciaObligatoria = true,
    string? Observaciones = null,
    WorkOrderEvidenceType TipoEvidencia = WorkOrderEvidenceType.Archivo,
    bool EsFoto = false,
    bool EsObligatoria = false,
    string? StorageProvider = null,
    string? LocalPath = null,
    string? OfflineId = null,
    string? SyncStatus = null);

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
    bool Completado = false,
    string? TemplateCode = null,
    WorkOrderChecklistResponseType TipoRespuesta = WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica,
    bool RequiereFoto = false,
    bool RequiereArchivo = false,
    bool RequiereFirma = false);

public sealed record UpdateChecklistItemRequest(
    bool Completado,
    string Reason,
    string? Respuesta = null,
    decimal? ValorNumerico = null,
    string? Texto = null,
    string? EvidenciaId = null,
    string? FirmaId = null);

public sealed record ApplyChecklistTemplateRequest(
    string CodigoTarea,
    string TemplateCode);

public sealed record RegisterWorkOrderSignatureRequest(
    string? SignatureFileKey = null,
    string? UsuarioId = null,
    string? Comentario = null,
    string? CodigoTarea = null,
    string Scope = "OT",
    string? SignatureImageDataUrl = null);

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
    int BloqueosCierre,
    string? UnidadOperativaCodigo,
    string? UnidadOperativaNombre,
    IReadOnlyCollection<WorkOrderAssetResponse> ActivosRelacionados,
    MaintenanceTargetSummary? Objetivo = null);

public sealed record WorkOrderDetailResponse(
    WorkOrderSummaryResponse Summary,
    IReadOnlyCollection<WorkOrderTaskResponse> Tasks,
    IReadOnlyCollection<WorkOrderTechnicianResponse> Technicians,
    IReadOnlyCollection<WorkOrderLaborResponse> Labor,
    IReadOnlyCollection<WorkOrderEvidenceResponse> Evidences,
    IReadOnlyCollection<WorkOrderSparePartResponse> SpareParts,
    IReadOnlyCollection<WorkOrderChecklistItemResponse> Checklist,
    IReadOnlyCollection<WorkOrderSignatureResponse> Signatures,
    IReadOnlyCollection<WorkOrderStatusHistoryResponse> History,
    IReadOnlyCollection<WorkOrderClosureBlocker> ClosureBlockers);

public sealed record WorkOrderAssetInput(string ActivoCodigo, string Rol = "AFECTADO");
public sealed record WorkOrderAssetResponse(string ActivoCodigo, string ActivoNombre, string Rol, bool EsPrincipal);
public sealed record WorkOrderTaskResponse(
    string NumeroOT,
    string CodigoTarea,
    string Descripcion,
    DateTimeOffset? FechaInicioProgramada,
    DateTimeOffset? FechaFinProgramada,
    bool RequiereEvidencia,
    bool RequiereHH,
    bool ChecklistObligatorio,
    string? Observaciones,
    string Titulo = "",
    string? CriterioAceptacion = null,
    decimal HorasEstimadas = 0,
    WorkOrderTaskStatus Estado = WorkOrderTaskStatus.PendienteAsignacion,
    WorkOrderTaskOrigin OrigenTarea = WorkOrderTaskOrigin.ManualSupervisor,
    DateTimeOffset? InicioRealUtc = null,
    DateTimeOffset? CompletadaTecnicoUtc = null,
    string? MotivoObservacion = null);

public sealed record WorkOrderLaborResponse(
    string HHId,
    string NumeroOT,
    string CodigoTarea,
    string TecnicoUserId,
    decimal Horas,
    string Descripcion,
    DateTimeOffset FechaTrabajo,
    string RegistradoPor,
    DateTimeOffset? HoraInicio,
    DateTimeOffset? HoraTermino,
    string? Comentario,
    bool ValidadoSupervisor,
    string? ValidadoPor,
    DateTimeOffset? ValidadoEnUtc);

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
    string? Observaciones,
    WorkOrderEvidenceType TipoEvidencia,
    bool EsFoto,
    bool EsObligatoria,
    string? StorageProvider,
    string? LocalPath,
    string? OfflineId,
    string? SyncStatus);

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
    string? CompletadoPor,
    string? TemplateCode,
    WorkOrderChecklistResponseType TipoRespuesta,
    string? Respuesta,
    decimal? ValorNumerico,
    string? Texto,
    string? EvidenciaId,
    bool RequiereFoto,
    bool RequiereArchivo,
    bool RequiereFirma,
    string? FirmaId);

public sealed record WorkOrderSignatureResponse(
    string FirmaId,
    string NumeroOT,
    string UsuarioId,
    string? SignatureFileKey,
    DateTimeOffset FirmadoEnUtc,
    string? Comentario,
    string? CodigoTarea,
    string Scope,
    string? SignatureImageDataUrl);

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
