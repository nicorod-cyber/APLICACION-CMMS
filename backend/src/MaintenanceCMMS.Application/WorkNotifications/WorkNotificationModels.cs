namespace MaintenanceCMMS.Application.WorkNotifications;

public enum WorkNotificationType
{
    Falla = 0,
    CondicionDetectada = 1,
    Documental = 2,
    Preventivo = 3,
    Mejora = 4,
    Inspeccion = 5,
    ApoyoOperacional = 6
}

public enum WorkNotificationStatus
{
    Creado = 0,
    EnEvaluacion = 1,
    Aprobado = 2,
    Rechazado = 3,
    ConvertidoOT = 4,
    Anulado = 5
}

public enum WorkNotificationPriority
{
    Baja = 0,
    Media = 1,
    Alta = 2,
    Critica = 3
}

public enum WorkNotificationCriticality
{
    Baja = 0,
    Media = 1,
    Alta = 2,
    Critica = 3
}

public enum WorkFailureClassification
{
    ConDetencion = 0,
    SinDetencion = 1,
    ConRestriccion = 2,
    DocumentalHabilitante = 3,
    Repetitiva = 4
}

public sealed record WorkNotificationQuery(
    WorkNotificationStatus? Status = null,
    WorkNotificationType? Type = null,
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    WorkNotificationPriority? Priority = null,
    bool IncludeClosed = false,
    bool SupervisorInbox = false,
    string? UnidadOperativaCodigo = null);

public sealed record CreateWorkNotificationRequest(
    WorkNotificationType Tipo,
    string Descripcion,
    WorkNotificationPriority Prioridad,
    WorkNotificationCriticality Criticidad,
    WorkFailureClassification ClasificacionFalla,
    string? FaenaCodigo = null,
    string? ActivoCodigo = null,
    string? Sistema = null,
    string? Subsistema = null,
    string? Componente = null,
    string? EvidenciaInicial = null,
    DateTimeOffset? FechaDeteccion = null,
    string? UnidadOperativaCodigo = null);

public sealed record WorkNotificationActionRequest(string Reason);

public sealed record ConvertWorkNotificationToWorkOrderRequest(
    string Reason,
    DateTimeOffset? FechaProgramada = null,
    string? TipoMantenimiento = null);

public sealed record WorkNotificationResponse(
    string AvisoId,
    WorkNotificationStatus Estado,
    WorkNotificationType Tipo,
    string FaenaCodigo,
    string? ActivoCodigo,
    string? UnidadOperativaCodigo,
    string? Sistema,
    string? Subsistema,
    string? Componente,
    string Descripcion,
    WorkNotificationPriority Prioridad,
    WorkNotificationCriticality Criticidad,
    string Solicitante,
    string? EvidenciaInicial,
    DateTimeOffset FechaDeteccion,
    DateTimeOffset FechaCreacion,
    WorkFailureClassification ClasificacionFalla,
    string? EvaluadoPor,
    DateTimeOffset? EvaluadoEnUtc,
    string? AprobadoPor,
    DateTimeOffset? AprobadoEnUtc,
    string? RechazadoPor,
    DateTimeOffset? RechazadoEnUtc,
    string? MotivoRechazo,
    string? AnuladoPor,
    DateTimeOffset? AnuladoEnUtc,
    string? MotivoAnulacion,
    string? NumeroOT,
    string? ConvertidoPor,
    DateTimeOffset? ConvertidoEnUtc,
    string? Observaciones);

public sealed record WorkNotificationConversionResponse(
    WorkNotificationResponse Aviso,
    string NumeroOT);
