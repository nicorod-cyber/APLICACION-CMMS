using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Application.Assets;

public sealed record AssetListQuery(
    string? FaenaCodigo = null,
    AssetStatus? Estado = null,
    string? Familia = null,
    string? Criticidad = null);

public sealed record CreateAssetRequest(
    string Codigo,
    string Nombre,
    string FaenaCodigo,
    string TipoActivo,
    AssetStatus Estado = AssetStatus.Active,
    string? UbicacionTecnicaCodigo = null,
    string? Familia = null,
    string? Marca = null,
    string? Modelo = null,
    string? Patente = null,
    string? NumeroSerie = null,
    string? Propiedad = null,
    string? Criticidad = null,
    string? EstadoDocumental = null,
    string? EstadoOperacional = null,
    IReadOnlyDictionary<string, string?>? TechnicalFields = null,
    bool FichaValidada = false);

public sealed record UpdateAssetRequest(
    string Nombre,
    string FaenaCodigo,
    string TipoActivo,
    AssetStatus Estado,
    string? UbicacionTecnicaCodigo = null,
    string? Familia = null,
    string? Marca = null,
    string? Modelo = null,
    string? Patente = null,
    string? NumeroSerie = null,
    string? Propiedad = null,
    string? Criticidad = null,
    string? EstadoDocumental = null,
    string? EstadoOperacional = null,
    IReadOnlyDictionary<string, string?>? TechnicalFields = null,
    bool? FichaValidada = null,
    string? Reason = null);

public sealed record CreateAssetStateEventRequest(
    AssetStatus Status,
    string Reason,
    DateTimeOffset? OccurredAtUtc = null);

public sealed record AssetSummary(
    string Codigo,
    string Nombre,
    string FaenaCodigo,
    string TipoActivo,
    AssetStatus Estado,
    string? UbicacionTecnicaCodigo,
    string? Familia,
    string? Marca,
    string? Modelo,
    string? Patente,
    string? NumeroSerie,
    string? Propiedad,
    string? Criticidad,
    string EstadoDocumental,
    string EstadoOperacional,
    AssetCompleteness CompletitudFicha,
    bool DisponibleDocumentalmente,
    bool FichaValidada);

public sealed record AssetDetail(
    string Codigo,
    string Nombre,
    string FaenaCodigo,
    string TipoActivo,
    AssetStatus Estado,
    string? UbicacionTecnicaCodigo,
    string? Familia,
    string? Marca,
    string? Modelo,
    string? Patente,
    string? NumeroSerie,
    string? Propiedad,
    string? Criticidad,
    string EstadoDocumental,
    string EstadoOperacional,
    AssetCompleteness CompletitudFicha,
    bool DisponibleDocumentalmente,
    bool FichaValidada,
    DateTimeOffset? FechaAlta,
    DateTimeOffset? FechaActualizacion,
    IReadOnlyDictionary<string, string?> TechnicalFields,
    IReadOnlyCollection<AssetWorkOrderSummary> WorkOrders,
    IReadOnlyCollection<CompatibleSparePartSummary> RepuestosCompatibles);

public sealed record AssetCompleteness(
    int RequiredFields,
    int CompletedFields,
    int Percentage,
    string State,
    IReadOnlyCollection<string> MissingFields);

public sealed record AssetStateEventResponse(
    string EventoId,
    string ActivoCodigo,
    AssetStatus EstadoAnterior,
    AssetStatus Estado,
    DateTimeOffset FechaEvento,
    string Motivo,
    string UsuarioId);

public sealed record AssetHistoryEntry(
    string Id,
    DateTimeOffset OccurredAtUtc,
    string Action,
    string Source,
    string UserId,
    string? PreviousValue,
    string? NewValue,
    string? Detail);

public sealed record AssetDocumentResponse(
    string EntidadTipo,
    string EntidadCodigo,
    string TipoDocumento,
    string Estado,
    DateOnly? FechaVencimiento,
    string? ArchivoKey,
    bool Critico,
    bool Vencido,
    bool BloqueaDisponibilidad);

public sealed record AssetCostLine(
    string Source,
    string TipoCosto,
    decimal Amount,
    string Currency,
    string? Reference);

public sealed record AssetCostSummary(
    string ActivoCodigo,
    decimal Total,
    string Currency,
    IReadOnlyCollection<AssetCostLine> Items);

public sealed record AssetAvailabilityResponse(
    string ActivoCodigo,
    bool Disponible,
    bool DisponibleOperacionalmente,
    bool DisponibleDocumentalmente,
    string EstadoOperacional,
    string EstadoDocumental,
    IReadOnlyCollection<string> Bloqueos,
    decimal PorcentajeDisponibilidad);

public sealed record AssetWorkOrderSummary(
    string NumeroOT,
    string Estado,
    string TipoMantenimiento,
    string? Descripcion,
    DateOnly? FechaProgramada);

public sealed record CompatibleSparePartSummary(
    string Codigo,
    string Descripcion,
    string? Familia,
    string? UnidadMedida);
