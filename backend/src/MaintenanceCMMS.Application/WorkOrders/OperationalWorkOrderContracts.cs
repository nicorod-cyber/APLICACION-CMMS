namespace MaintenanceCMMS.Application.WorkOrders;

public sealed record AssignWorkOrderSupervisorRequest(
    Guid SupervisorUsuarioId,
    string? MotivoReasignacion = null);

public sealed record AssignWorkOrderTechnicianRequest(Guid TecnicoUsuarioId);

public sealed record AssignWorkOrderTechniciansRequest(IReadOnlyCollection<Guid> TecnicoUsuarioIds);

public sealed record UnassignWorkOrderTechnicianRequest(string MotivoDesasignacion);

public sealed record UpdateWorkOrderTaskRequest(string Titulo, string Descripcion, string? CriterioAceptacion, decimal HorasEstimadas, DateTimeOffset? InicioProgramadoUtc, DateTimeOffset? FinProgramadoUtc, string? Observaciones);

public sealed record RegisterOwnLaborRequest(DateOnly FechaTrabajo, TimeOnly? HoraInicio, TimeOnly? HoraFin, decimal? Horas, string TipoHora, string DescripcionTrabajo);
public sealed record UpdateOwnLaborRequest(DateOnly FechaTrabajo, TimeOnly? HoraInicio, TimeOnly? HoraFin, decimal? Horas, string TipoHora, string DescripcionTrabajo, string Motivo);
public sealed record VoidWorkOrderLaborRequest(string Motivo);
public sealed record UploadWorkOrderEvidenceRequest(string Tipo, string? Descripcion, DateTimeOffset? FechaCapturaUtc);
public sealed record VoidWorkOrderEvidenceRequest(string Motivo);
public sealed record RegisterOwnWorkOrderSignatureRequest(string? Comentario);

public sealed record WorkOrderTaskActionRequest(string? Motivo = null);

public sealed record ObserveWorkOrderTaskRequest(string Motivo);

public sealed record WorkOrderUserLookupResponse(
    Guid Id,
    string Username,
    string Nombre,
    string Correo);

public sealed record WorkOrderSupervisorResponse(
    Guid? UsuarioId,
    string? Nombre,
    DateTimeOffset? AsignadoEnUtc);

public sealed record WorkOrderTechnicianResponse(
    Guid UsuarioId,
    string Nombre,
    DateTimeOffset AsignadoEnUtc,
    bool Vigente,
    DateTimeOffset? DesasignadoEnUtc,
    string? MotivoDesasignacion);

public sealed record MyAssignedWorkOrderResponse(
    WorkOrderSummaryResponse Orden,
    int TareasCompletadas,
    decimal PorcentajeAvance,
    decimal HorasPropias,
    bool FirmaVigente);
