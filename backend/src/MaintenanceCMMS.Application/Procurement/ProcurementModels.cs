namespace MaintenanceCMMS.Application.Procurement;

public enum ProcurementRequestStatus
{
    EnviadaAbastecimiento = 0,
    OCAsociada = 1,
    RecepcionParcial = 2,
    Recepcionada = 3,
    Entregada = 4,
    Cerrada = 5,
    Cancelada = 6
}

public sealed record SupplierQuery(
    string? Search = null,
    bool IncludeInactive = false);

public sealed record SupplierResponse(
    string Rut,
    string Nombre,
    string? Contacto,
    string? Email,
    string? Telefono,
    string? Direccion,
    int LeadTimeEsperadoDias,
    bool Activo,
    string? Observaciones);

public sealed record UpsertSupplierRequest(
    string Rut,
    string Nombre,
    string? Contacto = null,
    string? Email = null,
    string? Telefono = null,
    string? Direccion = null,
    int? LeadTimeEsperadoDias = null,
    bool Activo = true,
    string? Observaciones = null);

public sealed record ProcurementRequestQuery(
    ProcurementRequestStatus? Status = null,
    string? SupplierRut = null,
    string? FaenaCodigo = null,
    string? RepuestoCodigo = null,
    string? SolicitudInternaCmms = null,
    bool IncludeClosed = false,
    bool OverdueOnly = false);

public sealed record CreateProcurementRequestRequest(
    string Descripcion,
    decimal Cantidad,
    string Unidad,
    string Motivo,
    string? SolicitudInternaCmms = null,
    string? SolicitudExternaNumero = null,
    string? RepuestoCodigo = null,
    string? FaenaCodigo = null,
    string? BodegaCodigo = null,
    string? OtNumero = null,
    string? ActivoCodigo = null,
    DateTimeOffset? FechaSolicitudTecnica = null,
    DateTimeOffset? FechaAprobacionMantenimiento = null,
    DateTimeOffset? FechaEnvioAbastecimiento = null,
    decimal? CostoEstimado = null,
    string? Moneda = null,
    string? DocumentoRespaldoUrl = null);

public sealed record LinkPurchaseOrderRequest(
    string OcNumero,
    string ProveedorRut,
    DateTimeOffset FechaComprometida,
    string Reason,
    string? SolicitudExternaNumero = null,
    DateTimeOffset? FechaOC = null,
    decimal? CostoOC = null,
    string? Moneda = null,
    string? DocumentoOcUrl = null);

public sealed record RegisterProcurementReceptionRequest(
    decimal CantidadRecibida,
    string BodegaCodigo,
    string Reason,
    DateTimeOffset? FechaRecepcion = null,
    bool DespachoDirectoOt = false,
    string? OtNumero = null,
    string? ActivoCodigo = null,
    string? FaenaCodigo = null,
    DateTimeOffset? FechaEntrega = null,
    decimal? CostoReal = null,
    string? DocumentoRecepcionUrl = null,
    string? DocumentoEntregaUrl = null);

public sealed record DeliverProcurementRequest(
    decimal CantidadEntregada,
    string BodegaCodigo,
    string Reason,
    string? OtNumero = null,
    string? ActivoCodigo = null,
    string? FaenaCodigo = null,
    DateTimeOffset? FechaEntrega = null,
    string? DocumentoEntregaUrl = null);

public sealed record LeadTimeBreakdown(
    int? SolicitudAprobacionDias,
    int? AprobacionEnvioDias,
    int? EnvioOCDias,
    int? OCRecepcionDias,
    int? RecepcionEntregaDias,
    int? TotalDias);

public sealed record ProcurementRequestResponse(
    string SolicitudId,
    ProcurementRequestStatus Estado,
    string? SolicitudInternaCmms,
    string? SolicitudExternaNumero,
    string? OcNumero,
    string? ProveedorRut,
    string? ProveedorNombre,
    string? RepuestoCodigo,
    string Descripcion,
    decimal Cantidad,
    string Unidad,
    decimal CantidadRecibida,
    decimal CantidadEntregada,
    string? FaenaCodigo,
    string? BodegaCodigo,
    string? OtNumero,
    string? ActivoCodigo,
    string Motivo,
    DateTimeOffset FechaSolicitudTecnica,
    DateTimeOffset? FechaAprobacionMantenimiento,
    DateTimeOffset FechaEnvioAbastecimiento,
    DateTimeOffset? FechaOC,
    DateTimeOffset? FechaComprometida,
    DateTimeOffset? FechaRecepcion,
    DateTimeOffset? FechaEntrega,
    decimal? CostoEstimado,
    decimal? CostoOC,
    decimal? CostoReal,
    string Moneda,
    string? DocumentoRespaldoUrl,
    string? DocumentoOcUrl,
    string? DocumentoRecepcionUrl,
    string? DocumentoEntregaUrl,
    LeadTimeBreakdown LeadTime,
    bool EstaVencida,
    string CreadoPor,
    DateTimeOffset CreadoEnUtc,
    string? ActualizadoPor,
    DateTimeOffset? ActualizadoEnUtc,
    string? Observaciones);

public sealed record PurchaseOrderReferenceResponse(
    string OrdenCompraId,
    string SolicitudId,
    string OcNumero,
    string ProveedorRut,
    DateTimeOffset FechaOC,
    DateTimeOffset FechaComprometida,
    decimal? CostoOC,
    string Moneda,
    string? DocumentoOcUrl);

public sealed record ProcurementReceiptResponse(
    string RecepcionId,
    string SolicitudId,
    string? OcNumero,
    DateTimeOffset FechaRecepcion,
    decimal CantidadRecibida,
    decimal CantidadDespachada,
    string BodegaCodigo,
    bool DespachoDirectoOt,
    string? MovimientoRecepcionId,
    string? MovimientoEntregaId,
    decimal? CostoReal,
    string? DocumentoRecepcionUrl,
    string? DocumentoEntregaUrl);
