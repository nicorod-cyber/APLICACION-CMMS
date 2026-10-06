namespace MaintenanceCMMS.Application.WorkNotifications;

/// <summary>Lifecycle owned by Avisos. State changes are only permitted through explicit actions.</summary>
public enum WorkNotificationStatus { Borrador, PendientePlanificacion, DevueltoFaena, EnGestion, Cerrado, Rechazado, Anulado }
public enum NotificationOperationalState { Operativo, OperativoConAlerta, FueraDeServicio }
public enum WorkNotificationItemStatus { PendientePlanificacion, AprobadoParaGestion, AsociadoOT, ResueltoSinOT, Rechazado, Anulado }
public enum NotificationComponentRole { Fabrica, Chasis }
// Catalog compatibility: these values remain used by other maintenance modules and legacy rows.
public enum WorkNotificationType { Falla, CondicionDetectada, Documental, Preventivo, Mejora, Inspeccion, ApoyoOperacional }
public enum WorkNotificationPriority { Baja, Media, Alta, Critica }
public enum WorkNotificationCriticality { Baja, Media, Alta, Critica }
public enum WorkFailureClassification { ConDetencion, SinDetencion, ConRestriccion, DocumentalHabilitante, Repetitiva }

public sealed record WorkNotificationQuery(WorkNotificationStatus? Status = null, string? FaenaCodigo = null, string? EquipoCodigo = null, DateTimeOffset? DesdeUtc = null, DateTimeOffset? HastaUtc = null, string? Texto = null);
public sealed record CreateWorkNotificationItemRequest(string Descripcion, string? Observaciones = null, NotificationComponentRole? RolComponente = null, string? TechnicalSystemId = null, string? TechnicalSubsystemId = null, string? TechnicalComponentId = null, IReadOnlyCollection<string>? EvidenciaFileIds = null);
public sealed record CreateWorkNotificationRequest(string? FaenaCodigo, string? ActivoCodigo = null, string? UnidadOperativaCodigo = null, DateTimeOffset? FechaDeteccion = null, NotificationOperationalState EstadoOperacional = NotificationOperationalState.Operativo, DateTimeOffset? FueraServicioDesde = null, string? RestriccionOperacional = null, decimal? LecturaMedidor = null, string? Observaciones = null, IReadOnlyCollection<CreateWorkNotificationItemRequest>? Trabajos = null);
public sealed record UpdateWorkNotificationDraftRequest(DateTimeOffset? FechaDeteccion, NotificationOperationalState EstadoOperacional, DateTimeOffset? FueraServicioDesde, string? RestriccionOperacional, decimal? LecturaMedidor, string? Observaciones, IReadOnlyCollection<CreateWorkNotificationItemRequest> Trabajos);
public sealed record WorkNotificationActionRequest(string Motivo);

/// <summary>Raised when an Aviso changed after it was loaded for a state-changing operation.</summary>
public sealed class WorkNotificationConcurrencyException : Exception
{
    public WorkNotificationConcurrencyException() : base("El aviso fue modificado por otro usuario.") { }
}

public sealed record WorkNotificationEvidenceResponse(string Id, string FileId, string NombreArchivo, DateTimeOffset CreadoEnUtc);
public sealed record WorkNotificationItemResponse(string Id, int Secuencia, string AffectedAssetId, string AffectedAssetCodeSnapshot, string AffectedAssetNameSnapshot, NotificationComponentRole? RolComponente, string? TechnicalSystemId, string? TechnicalSubsystemId, string? TechnicalComponentId, string Descripcion, string? Observaciones, WorkNotificationItemStatus Estado, DateTimeOffset CreadoEnUtc, IReadOnlyCollection<WorkNotificationEvidenceResponse> Evidencias, string? WorkOrderId = null, string? WorkOrderTaskId = null);
public sealed record WorkNotificationHistoryResponse(string Id, WorkNotificationStatus? EstadoOrigen, WorkNotificationStatus EstadoDestino, string UsuarioId, DateTimeOffset FechaUtc, string? Motivo);
public sealed record WorkNotificationResponse(string AvisoId, WorkNotificationStatus Estado, string FaenaCodigo, string? ActivoCodigo, string? UnidadOperativaCodigo, DateTimeOffset FechaDeteccion, NotificationOperationalState EstadoOperacional, DateTimeOffset? FueraServicioDesde, string? RestriccionOperacional, decimal? LecturaMedidor, string? UnidadMedicion, string? Observaciones, string CreadoPorUsuarioId, DateTimeOffset CreadoEnUtc, IReadOnlyCollection<WorkNotificationItemResponse> Trabajos, IReadOnlyCollection<WorkNotificationHistoryResponse> Historial);
