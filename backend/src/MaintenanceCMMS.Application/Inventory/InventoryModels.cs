using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Application.Inventory;

public enum SparePartStatus
{
    Activo = 0,
    Obsoleto = 1,
    Bloqueado = 2,
    Reemplazado = 3
}

public enum WarehouseType
{
    Central = 0,
    Taller = 1,
    Faena = 2,
    Transito = 3
}

public enum StockReservationStatus
{
    Activa = 0,
    ParcialmenteEntregada = 1,
    Entregada = 2,
    Liberada = 3,
    Cancelada = 4
}

public enum StockTransferStatus
{
    EnTransito = 0,
    Recibida = 1,
    Cancelada = 2
}

public sealed record SparePartQuery(
    string? Search = null,
    string? Familia = null,
    SparePartStatus? Estado = null,
    bool CriticalOnly = false,
    bool LowStockOnly = false,
    bool IncludeObsolete = false);

public sealed record WarehouseQuery(
    string? FaenaCodigo = null,
    WarehouseType? Tipo = null,
    bool IncludeInactive = false);

public sealed record StockQuery(
    string? BodegaCodigo = null,
    string? RepuestoCodigo = null,
    string? FaenaCodigo = null,
    bool LowStockOnly = false,
    bool CriticalOnly = false);

public sealed record StockMovementQuery(
    string? BodegaCodigo = null,
    string? RepuestoCodigo = null,
    StockMovementType? Type = null,
    string? ReferenceType = null,
    string? ReferenceId = null,
    int Take = 100);

public sealed record CreateSparePartRequest(
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
    decimal? CostoUnitarioPromedio = null,
    SparePartStatus Estado = SparePartStatus.Activo,
    string? ProveedorPreferente = null,
    string? ReemplazoCodigo = null);

public sealed record UpdateSparePartRequest(
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
    decimal? CostoUnitarioPromedio = null,
    SparePartStatus Estado = SparePartStatus.Activo,
    string? ProveedorPreferente = null,
    string? ReemplazoCodigo = null,
    string? Reason = null);

public sealed record CreateWarehouseRequest(
    string Codigo,
    string Nombre,
    string FaenaCodigo,
    WarehouseType Tipo,
    string? Ubicacion = null,
    IReadOnlyCollection<string>? UbicacionesInternas = null,
    bool Activa = true,
    string? Responsable = null,
    bool PermiteStockNegativo = false);

public sealed record StockMovementRequest(
    StockMovementType Type,
    string RepuestoCodigo,
    decimal Quantity,
    string Reason,
    string? BodegaCodigo = null,
    string? SourceWarehouseCode = null,
    string? TargetWarehouseCode = null,
    string? ReferenceType = null,
    string? ReferenceId = null,
    bool AllowNegativeException = false);

public sealed record CreateStockReservationRequest(
    string RepuestoCodigo,
    string BodegaCodigo,
    decimal Quantity,
    string WorkOrderId,
    string RequestedBy,
    string Reason);

public sealed record ReleaseStockReservationRequest(
    decimal Quantity,
    string Reason);

public sealed record DeliverMaterialRequest(
    string RepuestoCodigo,
    string BodegaCodigo,
    decimal Quantity,
    string Reason,
    string? WorkOrderId = null,
    string? AssetCode = null,
    string? FaenaCodigo = null,
    string? CostCenter = null,
    string? ReservationId = null);

public sealed record TransferStockRequest(
    string RepuestoCodigo,
    string SourceWarehouseCode,
    string TransitWarehouseCode,
    string TargetWarehouseCode,
    decimal Quantity,
    string Reason,
    string? TransferId = null);

public sealed record ReceiveTransferRequest(
    string Reason);

public sealed record ReturnStockRequest(
    string RepuestoCodigo,
    string BodegaCodigo,
    decimal Quantity,
    bool Reusable,
    string Reason,
    string? WorkOrderId = null,
    string? AssetCode = null);

public sealed record AdjustStockRequest(
    string RepuestoCodigo,
    string BodegaCodigo,
    decimal Quantity,
    string Reason,
    bool AllowNegativeException = false,
    bool RequiresSupervisorApproval = false,
    string? SupervisorApprovalUserId = null);

public sealed record WriteOffStockRequest(
    string RepuestoCodigo,
    string BodegaCodigo,
    decimal Quantity,
    string Reason,
    string? ReferenceType = null,
    string? ReferenceId = null,
    bool AllowNegativeException = false);

public sealed record WarehouseResponse(
    string Codigo,
    string Nombre,
    string FaenaCodigo,
    WarehouseType Tipo,
    string? Ubicacion,
    IReadOnlyCollection<string> UbicacionesInternas,
    bool Activa,
    string? Responsable,
    bool PermiteStockNegativo);

public sealed record SparePartSummary(
    string Codigo,
    string? CodigoSap,
    string? CodigoProveedor,
    string Descripcion,
    string DescripcionTecnica,
    string UnidadMedida,
    string? FamiliaEquipo,
    string? MarcaFabricante,
    string? ModeloReferencia,
    bool Critico,
    decimal StockMinimo,
    decimal StockMaximo,
    decimal PuntoReposicion,
    int LeadTimeEsperadoDias,
    decimal? CostoUnitarioPromedio,
    SparePartStatus Estado,
    bool EsNoCodificado,
    string? ProveedorPreferente,
    string? ReemplazoCodigo,
    decimal StockFisicoTotal,
    decimal StockReservadoTotal,
    decimal StockDisponibleTotal,
    bool BajoMinimo,
    bool CriticoSinStock);

public sealed record SparePartDetail(
    SparePartSummary Summary,
    IReadOnlyCollection<StockItemResponse> Stock,
    IReadOnlyCollection<StockMovementResponse> Movements);

public sealed record StockItemResponse(
    string BodegaCodigo,
    string BodegaNombre,
    string FaenaCodigo,
    string RepuestoCodigo,
    string RepuestoDescripcion,
    string UnidadMedida,
    bool RepuestoCritico,
    decimal StockFisico,
    decimal StockReservado,
    decimal StockDisponible,
    decimal StockMinimo,
    decimal StockMaximo,
    decimal PuntoReposicion,
    bool BajoMinimo,
    bool CriticoSinStock,
    DateTimeOffset? ActualizadoEnUtc);

public sealed record StockMovementResponse(
    string MovimientoId,
    DateTimeOffset FechaUtc,
    StockMovementType Type,
    string RepuestoCodigo,
    string? BodegaCodigo,
    string? BodegaOrigenCodigo,
    string? BodegaDestinoCodigo,
    decimal Quantity,
    decimal StockFisicoAnterior,
    decimal StockFisicoNuevo,
    decimal StockReservadoAnterior,
    decimal StockReservadoNuevo,
    string Motivo,
    string UsuarioId,
    string? ReferenceType,
    string? ReferenceId,
    bool PermiteNegativoExcepcional);

public sealed record StockReservationResponse(
    string ReservaId,
    StockReservationStatus Estado,
    DateTimeOffset FechaUtc,
    string RepuestoCodigo,
    string BodegaCodigo,
    decimal CantidadReservada,
    decimal CantidadEntregada,
    decimal CantidadLiberada,
    decimal CantidadPendiente,
    string WorkOrderId,
    string Solicitante,
    string Motivo,
    string UsuarioId);

public sealed record StockTransferResponse(
    string TransferenciaId,
    StockTransferStatus Estado,
    DateTimeOffset FechaSolicitudUtc,
    DateTimeOffset? FechaRecepcionUtc,
    string RepuestoCodigo,
    string BodegaOrigenCodigo,
    string BodegaTransitoCodigo,
    string BodegaDestinoCodigo,
    decimal Cantidad,
    string Motivo,
    string UsuarioId,
    string? RecibidoPor,
    string? MotivoRecepcion,
    IReadOnlyCollection<StockMovementResponse> Movements);

public sealed record StockAlertResponse(
    string AlertKey,
    string Severity,
    string RepuestoCodigo,
    string RepuestoDescripcion,
    string? BodegaCodigo,
    string Message);

public sealed record InventoryDashboardResponse(
    int TotalRepuestos,
    int RepuestosCriticos,
    int RepuestosNoCodificados,
    int RepuestosBajoMinimo,
    int CriticosSinStock,
    int TotalBodegas,
    decimal StockFisicoTotal,
    decimal StockDisponibleTotal,
    IReadOnlyCollection<StockAlertResponse> Alerts);
