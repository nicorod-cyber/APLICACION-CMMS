using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Application.Costs;

public enum PaymentStatus { Pendiente, Enviado, Aprobado, Rechazado, Pagado }
public enum CostCategory { Repuesto, ManoObra, ServicioExterno, Traslado, Otro }
public sealed record CostQuery(DateTimeOffset? Desde=null,DateTimeOffset? Hasta=null,string? OtNumero=null,string? ActivoCodigo=null,string? FaenaCodigo=null,string? ContratoCodigo=null,string? ProveedorRut=null,CostCategory? Categoria=null);
public sealed record CreateCostRequest(CostCategory Categoria, decimal Monto, string Descripcion, DateTimeOffset Fecha, string? OtNumero=null,string? ActivoCodigo=null,string? FaenaCodigo=null,string? ContratoCodigo=null,string? ProveedorRut=null,decimal? Cantidad=null,decimal? CostoUnitario=null,string Moneda="CLP",string? DocumentoUrl=null,string? RepuestoCodigo=null,string? MovimientoNumero=null,string? Especialidad=null);
public sealed record UpdateCostRequest(decimal Monto,string Descripcion,DateTimeOffset Fecha,string? DocumentoUrl=null,string? Reason=null);
public sealed record UpsertLaborRateRequest(string Codigo,decimal TarifaHora,string? Especialidad=null,bool Activa=true);
public sealed record CostResponse(string Numero,CostCategory Categoria,decimal Monto,string Moneda,string Descripcion,DateTimeOffset Fecha,string? OtNumero,string? ActivoCodigo,string? FaenaCodigo,string? ContratoCodigo,string? ProveedorRut,decimal? Cantidad,decimal? CostoUnitario,string? DocumentoUrl,string CreadoPor);
public sealed record CreatePaymentStatementRequest(string NumeroEstadoPago,string ProveedorRut,decimal Monto,string Moneda,string? ContratoCodigo=null,string? FaenaCodigo=null,string? DocumentoUrl=null);
public sealed record ChangePaymentStatusRequest(PaymentStatus Estado,string? Motivo=null);
public sealed record PaymentStatementResponse(string NumeroEstadoPago,string ProveedorRut,decimal Monto,string Moneda,PaymentStatus Estado,string? ContratoCodigo,string? FaenaCodigo,string? DocumentoUrl,DateTimeOffset CreadoEnUtc,string? ActualizadoPor,DateTimeOffset? ActualizadoEnUtc,string? MotivoRechazo);
public sealed record CostDashboardResponse(decimal Total,IReadOnlyDictionary<CostCategory,decimal> PorCategoria,IReadOnlyCollection<CostResponse> PorOt,IReadOnlyCollection<CostResponse> PorActivo,IReadOnlyCollection<CostResponse> PorProveedor,IReadOnlyCollection<CostResponse> PorContrato);
