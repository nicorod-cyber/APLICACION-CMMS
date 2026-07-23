using MaintenanceCMMS.Application.Abstractions.Pagination;

namespace MaintenanceCMMS.Application.Assets;

public sealed record AssetListQuery(
    string? FaenaCodigo = null,
    string? TipoActivoCodigo = null,
    string? FamiliaEquipoCodigo = null,
    string? Criticidad = null,
    string? EstadoOperacionalCodigo = null,
    string? Texto = null,
    int Page = 1,
    int PageSize = 25);

public sealed record AssetCatalogItem(string Codigo, string Nombre, string? TipoActivoCodigo = null, string? FaenaCodigo = null);
public sealed record AssetCatalogResponse(IReadOnlyCollection<AssetCatalogItem> TiposActivo, IReadOnlyCollection<AssetCatalogItem> FamiliasEquipo, IReadOnlyCollection<AssetCatalogItem> EstadosOperacionales, IReadOnlyCollection<AssetCatalogItem> UbicacionesTecnicas, IReadOnlyCollection<AssetCatalogItem> Criticidades);
public sealed record AssetAttributeValueInput(
    string DefinicionCodigo,
    string? ValorTexto = null,
    decimal? ValorNumerico = null,
    bool? ValorBooleano = null,
    DateOnly? ValorFecha = null,
    string? Observaciones = null);

public sealed record CreateAssetRequest(
    string Nombre,
    string TipoActivoCodigo,
    string? FamiliaEquipoCodigo,
    string? FaenaCodigo,
    string EstadoOperacionalCodigo,
    string? Marca = null,
    string? Modelo = null,
    string? NumeroSerie = null,
    string? Propiedad = null,
    string? Criticidad = null,
    short? AnioFabricacion = null,
    DateOnly? FechaAdquisicion = null,
    DateOnly? FechaPuestaServicio = null,
    DateOnly? FechaBaja = null,
    string? TipoMedicionUso = null,
    string? Observaciones = null,
    IReadOnlyCollection<AssetAttributeValueInput>? Atributos = null);

public sealed record UpdateAssetRequest(
    string Nombre,
    string TipoActivoCodigo,
    string? FamiliaEquipoCodigo,
    string? FaenaCodigo,
    string EstadoOperacionalCodigo,
    string? Marca = null,
    string? Modelo = null,
    string? NumeroSerie = null,
    string? Propiedad = null,
    string? Criticidad = null,
    short? AnioFabricacion = null,
    DateOnly? FechaAdquisicion = null,
    DateOnly? FechaPuestaServicio = null,
    DateOnly? FechaBaja = null,
    string? TipoMedicionUso = null,
    string? Observaciones = null,
    IReadOnlyCollection<AssetAttributeValueInput>? Atributos = null,
    string? Motivo = null);

public sealed record CreateAssetStateEventRequest(string EstadoOperacionalCodigo, string Motivo, DateTimeOffset? FechaEventoUtc = null, string? TipoAntecedente = null, string? AntecedenteId = null, string? ReferenciaAntecedente = null);

public sealed record TransferAssetRequest(
    string FaenaDestinoCodigo,
    DateTimeOffset FechaEfectivaUtc,
    string Motivo,
    string? Observaciones = null,
    bool TrasladarUnidadCompleta = false);

public sealed record CreateAssetReadingRequest(
    decimal Valor,
    DateTimeOffset? FechaLecturaUtc = null,
    string Origen = "MANUAL",
    string? EvidenciaReferencia = null,
    string? Observaciones = null);

public sealed record CorrectAssetReadingRequest(
    decimal Valor,
    string MotivoCorreccion,
    DateTimeOffset? FechaLecturaUtc = null,
    string Origen = "MANUAL",
    string? EvidenciaReferencia = null,
    string? Observaciones = null);

public sealed record AssetAttributeValueResponse(string DefinicionCodigo, string Nombre, string TipoDato, string? Unidad, string? ValorTexto, decimal? ValorNumerico, bool? ValorBooleano, DateOnly? ValorFecha, string? Observaciones);
public sealed record AssetAttributeDefinitionResponse(string Codigo, string Nombre, string TipoDato, string? Unidad, bool Obligatorio, bool EsIdentificador, bool EsUnico, bool PermiteBusqueda, bool PermiteFiltro, bool MostrarEnListado, decimal? ValorMinimo, decimal? ValorMaximo, string? PatronValidacion, string? OpcionesJson, string? GrupoVisualizacion, int OrdenVisualizacion);
public sealed record AssetReadingResponse(string Id, DateTimeOffset FechaLecturaUtc, decimal Valor, string Unidad, decimal? Avance, string Origen, bool EsCorreccion, string? LecturaCorregidaId, bool EsAnomala, string? MensajeValidacion, string? Observaciones);

public sealed record AssetCompleteness(int RequiredFields, int CompletedFields, int Percentage, string State, IReadOnlyCollection<string> MissingFields);

public sealed record AssetSummary(
    string Codigo, string Nombre, string TipoActivoCodigo, string TipoActivoNombre,
    string? FamiliaEquipoCodigo, string? FamiliaEquipoNombre, string? FaenaCodigo,
    string? UbicacionTecnicaCodigo, string EstadoOperacionalCodigo, string? Criticidad,
    string? TipoMedicionUso, decimal? UltimaLectura, string? UnidadLectura,
    AssetCompleteness CompletitudTecnica, string EstadoDocumental, bool DisponibleDocumentalmente);

public sealed record AssetDetail(
    AssetSummary Resumen, string? Marca, string? Modelo, string? NumeroSerie, string? Propiedad,
    short? AnioFabricacion, DateOnly? FechaAdquisicion, DateOnly? FechaPuestaServicio, DateOnly? FechaBaja,
    string? Observaciones, IReadOnlyCollection<AssetAttributeValueResponse> Atributos,
    IReadOnlyCollection<AssetAttributeDefinitionResponse> DefinicionesAplicables,
    IReadOnlyCollection<AssetReadingResponse> Lecturas, IReadOnlyCollection<AssetWorkOrderSummary> OrdenesTrabajo,
    string? UnidadOperativaVigente, IReadOnlyCollection<AssetCompositionHistoryEntry> HistorialComposicion,
    IReadOnlyCollection<AssetDocumentResponse> Documentos,
    IReadOnlyCollection<AssetTransferResponse>? HistorialTraslados = null,
    IReadOnlyCollection<AssetIdentifierAliasResponse>? IdentificadoresHistoricos = null);

public sealed record AssetStateEventResponse(string EventoId, string ActivoCodigo, string? EstadoAnteriorCodigo, string EstadoOperacionalCodigo, DateTimeOffset FechaEvento, string Motivo, string UsuarioId, string? TipoAntecedente = null, string? AntecedenteId = null, string? ReferenciaAntecedente = null);
public sealed record AssetStateEventAntecedentSearchItem(string Id, string Codigo, string Descripcion, DateTimeOffset? Fecha, string? Estado, string ActivoCodigo, string? FaenaCodigo, string? Detalle = null);
public sealed record AssetStateEventAntecedentSearchResponse(IReadOnlyCollection<AssetStateEventAntecedentSearchItem> Items, int Total, int Pagina, int TamanoPagina);
public sealed record AssetTransferResponse(string TrasladoId, string ActivoCodigo, string? FaenaOrigenCodigo, string? FaenaDestinoCodigo, DateTimeOffset FechaEfectivaUtc, string Motivo, string UsuarioId, DateTimeOffset FechaRegistroUtc, string? Observaciones, string? UnidadOperativaCodigo);
public sealed record AssetIdentifierAliasResponse(string TipoIdentificador, string Ambito, string Valor, DateTimeOffset VigenciaDesdeUtc, DateTimeOffset? VigenciaHastaUtc, bool Vigente);
public sealed record AssetHistoryEntry(string Id, DateTimeOffset OccurredAtUtc, string Action, string Source, string UserId, string? PreviousValue, string? NewValue, string? Detail);
public sealed record AssetDocumentResponse(string EntidadTipo, string EntidadCodigo, string TipoDocumento, string Estado, DateOnly? FechaVencimiento, string? ArchivoKey, bool Critico, bool Vencido, bool BloqueaDisponibilidad);
public sealed record AssetDocumentMatrixRow(string TipoDocumento, bool Obligatorio, bool Critico, bool BloqueaDisponibilidad, string Estado, string? DocumentoVigente, int? Version, DateOnly? FechaVencimiento, int? DiasParaVencer, string? MotivoPendencia);
public sealed record AssetCostLine(string Source, string TipoCosto, decimal Amount, string Currency, string? Reference);
public sealed record AssetCostSummary(string ActivoCodigo, decimal Total, string Currency, IReadOnlyCollection<AssetCostLine> Items);
public sealed record AssetAvailabilityResponse(string ActivoCodigo, bool Disponible, bool DisponibleOperacionalmente, bool DisponibleDocumentalmente, string EstadoOperacional, string EstadoDocumental, IReadOnlyCollection<string> Bloqueos, decimal PorcentajeDisponibilidad);
public sealed record AssetWorkOrderSummary(string NumeroOT, string Estado, string TipoMantenimiento, string? Descripcion, DateOnly? FechaProgramada);
public sealed record AssetCompositionHistoryEntry(string UnidadOperativaCodigo, string RolComponenteCodigo, DateTimeOffset FechaMontajeUtc, DateTimeOffset? FechaDesmontajeUtc, string? Observaciones);
public sealed record CompatibleSparePartSummary(string Codigo, string Descripcion, string? Familia, string? UnidadMedida);
