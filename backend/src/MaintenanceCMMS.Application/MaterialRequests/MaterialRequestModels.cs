namespace MaintenanceCMMS.Application.MaterialRequests;

public enum MaterialRequestStatus
{
    Solicitada = 0,
    PendienteAprobacionMantenimiento = 1,
    AprobadaPorMantenimiento = 2,
    EnRevisionBodega = 3,
    Reservada = 4,
    PendienteStock = 5,
    PendienteAbastecimiento = 6,
    EnPreparacion = 7,
    Entregada = 8,
    RecibidaPorTecnico = 9,
    Cerrada = 10,
    Rechazada = 11
}

public enum MaterialRequestSource
{
    OT = 0,
    Tarea = 1,
    Bodega = 2
}

public enum MaterialRequestType
{
    RepuestoCodificado = 0,
    MaterialNoCodificado = 1
}

public sealed record MaterialRequestQuery(
    MaterialRequestStatus? Status = null,
    MaterialRequestType? Type = null,
    MaterialRequestSource? Source = null,
    string? FaenaCodigo = null,
    string? Requester = null,
    bool IncludeClosed = false);

public sealed record CreateMaterialRequestRequest(
    MaterialRequestSource Source,
    MaterialRequestType Type,
    string DescripcionTecnica,
    decimal Cantidad,
    string Unidad,
    string Motivo,
    string? RepuestoCodigo = null,
    string? FotoReferencia = null,
    string? ActivoCodigo = null,
    string? OtNumero = null,
    string? TareaCodigo = null,
    string? FaenaCodigo = null,
    string? BodegaCodigo = null);

public sealed record MaterialRequestReasonRequest(string Reason);

public sealed record WarehouseReviewMaterialRequestRequest(
    string BodegaCodigo,
    string Reason);

public sealed record DeliverRequestedMaterialRequest(
    string BodegaCodigo,
    string Reason);

public sealed record ConvertMaterialRequestToSparePartRequest(
    string Descripcion,
    string UnidadMedida,
    string? CodigoSap = null,
    string? CodigoProveedor = null,
    string? DescripcionTecnica = null,
    string? FamiliaEquipo = null,
    string? MarcaFabricante = null,
    string? ModeloReferencia = null,
    bool Critico = false,
    decimal? StockMinimo = null,
    decimal? StockMaximo = null,
    decimal? PuntoReposicion = null,
    int? LeadTimeEsperadoDias = null,
    string? ProveedorPreferente = null);

public sealed record MaterialRequestResponse(
    string NumeroSolicitud,
    MaterialRequestStatus Estado,
    MaterialRequestType Tipo,
    MaterialRequestSource Origen,
    string Solicitante,
    DateTimeOffset SolicitadoEnUtc,
    string DescripcionTecnica,
    decimal Cantidad,
    string Unidad,
    string Motivo,
    string? RepuestoCodigo,
    string? RepuestoMaestroCodigo,
    string? FotoReferencia,
    string? ActivoCodigo,
    string? OtNumero,
    string? TareaCodigo,
    string? FaenaCodigo,
    string? BodegaCodigo,
    string? ReservaId,
    string? MovimientoEntregaId,
    string? StockDecision,
    string? AprobadorMantenimiento,
    DateTimeOffset? AprobadoMantenimientoEnUtc,
    string? AprobadorBodega,
    DateTimeOffset? AprobadoBodegaEnUtc,
    string? RechazadoPor,
    DateTimeOffset? RechazadoEnUtc,
    string? MotivoRechazo,
    string? RecibidoPor,
    DateTimeOffset? RecibidoEnUtc,
    string? ConvertidoPor,
    DateTimeOffset? ConvertidoEnUtc,
    DateTimeOffset? CerradoEnUtc,
    string? Observaciones);
