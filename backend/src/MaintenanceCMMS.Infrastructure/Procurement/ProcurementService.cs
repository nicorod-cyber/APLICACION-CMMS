using System.Text.Json;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.Procurement;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Procurement;

/// <summary>PostgreSQL-backed procurement aggregate. Excel is intentionally not a runtime dependency.</summary>
public sealed class ProcurementService : IProcurementService
{
    private readonly CmmsDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IAuditService _audit;

    public ProcurementService(CmmsDbContext dbContext, IInventoryService inventoryService, IAuditService auditService)
    {
        _db = dbContext;
        _inventory = inventoryService;
        _audit = auditService;
    }

    public async Task<IReadOnlyCollection<SupplierResponse>> ListSuppliersAsync(SupplierQuery query, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanView(user);
        var suppliers = _db.Suppliers.AsNoTracking().AsQueryable();
        if (!query.IncludeInactive) suppliers = suppliers.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            suppliers = suppliers.Where(x => EF.Functions.ILike(x.TaxId, $"%{search}%") || EF.Functions.ILike(x.Name, $"%{search}%") || (x.Contact != null && EF.Functions.ILike(x.Contact, $"%{search}%")));
        }
        return (await suppliers.OrderBy(x => x.Name).ToListAsync(ct)).Select(ToResponse).ToArray();
    }

    public async Task<SupplierResponse?> GetSupplierAsync(string rut, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanView(user);
        var supplier = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.TaxId == NormalizeCode(rut), ct);
        return supplier is null ? null : ToResponse(supplier);
    }

    public async Task<SupplierResponse> CreateSupplierAsync(UpsertSupplierRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user); ValidateSupplier(request);
        var taxId = NormalizeCode(request.Rut)!;
        if (await _db.Suppliers.AnyAsync(x => x.TaxId == taxId, ct)) throw new DomainException($"Ya existe el proveedor '{taxId}'.");
        var entity = Apply(request, new SupplierEntity { TaxId = taxId });
        _db.Suppliers.Add(entity);
        await _db.SaveChangesAsync(ct);
        await AuditAsync(user, "procurement.supplier_created", "Supplier", taxId, entity, null, request.Observaciones, ct);
        return ToResponse(entity);
    }

    public async Task<SupplierResponse?> UpdateSupplierAsync(string rut, UpsertSupplierRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user); ValidateSupplier(request);
        var entity = await _db.Suppliers.SingleOrDefaultAsync(x => x.TaxId == NormalizeCode(rut), ct);
        if (entity is null) return null;
        var taxId = NormalizeCode(request.Rut)!;
        if (taxId != entity.TaxId && await _db.Suppliers.AnyAsync(x => x.TaxId == taxId, ct)) throw new DomainException($"Ya existe el proveedor '{taxId}'.");
        entity = Apply(request, entity); entity.TaxId = taxId; entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(user, "procurement.supplier_updated", "Supplier", taxId, entity, null, request.Observaciones, ct);
        return ToResponse(entity);
    }

    public async Task<IReadOnlyCollection<ProcurementRequestResponse>> ListRequestsAsync(ProcurementRequestQuery query, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanView(user);
        var requests = RequestQuery();
        if (!query.IncludeClosed) requests = requests.Where(x => x.Status != (int)ProcurementRequestStatus.Cerrada && x.Status != (int)ProcurementRequestStatus.Cancelada);
        if (query.Status is not null) requests = requests.Where(x => x.Status == (int)query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.SupplierRut)) requests = requests.Where(x => x.PurchaseOrders.Any(po => po.Supplier.TaxId == NormalizeCode(query.SupplierRut)));
        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo)) requests = requests.Where(x => x.Faena != null && x.Faena.Code == NormalizeCode(query.FaenaCodigo));
        if (!string.IsNullOrWhiteSpace(query.RepuestoCodigo)) requests = requests.Where(x => x.Lines.Any(l => l.SparePart != null && l.SparePart.Code == NormalizeCode(query.RepuestoCodigo)));
        if (!string.IsNullOrWhiteSpace(query.SolicitudInternaCmms)) requests = requests.Where(x => x.MaterialRequest != null && x.MaterialRequest.RequestNumber == NormalizeCode(query.SolicitudInternaCmms));
        var results = (await requests.OrderByDescending(x => x.SentToProcurementAtUtc).ToListAsync(ct)).Select(ToResponse).Where(x => CanAccessFaena(user, x.FaenaCodigo));
        return query.OverdueOnly ? results.Where(x => x.EstaVencida).ToArray() : results.ToArray();
    }

    public async Task<ProcurementRequestResponse?> GetRequestAsync(string id, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanView(user);
        var entity = await RequestQuery().SingleOrDefaultAsync(x => x.RequestNumber == NormalizeCode(id), ct);
        if (entity is null) return null;
        var response = ToResponse(entity); EnsureFaenaAccess(user, response.FaenaCodigo); return response;
    }

    public async Task<ProcurementRequestResponse> CreateRequestAsync(CreateProcurementRequestRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user); EnsurePositive(request.Cantidad); Required(request.Descripcion, nameof(request.Descripcion)); Required(request.Unidad, nameof(request.Unidad)); Required(request.Motivo, nameof(request.Motivo));
        var faena = await FindOptionalAsync(_db.Faenas, request.FaenaCodigo, x => x.Code, "faena", ct); EnsureFaenaAccess(user, faena?.Code);
        var warehouse = await FindOptionalAsync(_db.Warehouses, request.BodegaCodigo, x => x.Code, "bodega", ct);
        var asset = await FindOptionalAsync(_db.Assets, request.ActivoCodigo, x => x.Code, "activo", ct);
        var workOrder = await FindOptionalAsync(_db.WorkOrders, request.OtNumero, x => x.WorkOrderNumber, "OT", ct);
        var sparePart = await FindOptionalAsync(_db.SpareParts, request.RepuestoCodigo, x => x.Code, "repuesto", ct);
        var materialRequest = string.IsNullOrWhiteSpace(request.SolicitudInternaCmms) ? null : await _db.MaterialRequests.SingleOrDefaultAsync(x => x.RequestNumber == NormalizeCode(request.SolicitudInternaCmms), ct);
        var now = DateTimeOffset.UtcNow;
        var entity = new ProcurementRequestEntity
        {
            RequestNumber = await NextRequestNumberAsync(ct), Status = (int)ProcurementRequestStatus.EnviadaAbastecimiento,
            MaterialRequestId = materialRequest?.Id, FaenaId = faena?.Id, WarehouseId = warehouse?.Id, AssetId = asset?.Id, WorkOrderId = workOrder?.Id,
            Reason = request.Motivo.Trim(), TechnicalRequestedAtUtc = request.FechaSolicitudTecnica ?? now, MaintenanceApprovedAtUtc = request.FechaAprobacionMantenimiento,
            SentToProcurementAtUtc = request.FechaEnvioAbastecimiento ?? now, CreatedByUserId = user.UserId
        };
        entity.Lines.Add(new ProcurementRequestLineEntity
        {
            SparePartId = sparePart?.Id, ExternalRequestNumber = Text(request.SolicitudExternaNumero), Description = request.Descripcion.Trim(), RequestedQuantity = request.Cantidad,
            Unit = request.Unidad.Trim(), EstimatedCost = request.CostoEstimado, Currency = Text(request.Moneda) ?? "CLP", SupportingDocumentUrl = Text(request.DocumentoRespaldoUrl), Notes = "Solicitud enviada a abastecimiento"
        });
        _db.ProcurementRequests.Add(entity); await _db.SaveChangesAsync(ct);
        var response = ToResponse(await RequestQuery().SingleAsync(x => x.Id == entity.Id, ct));
        await AuditAsync(user, "procurement.request_created", "ProcurementRequest", response.SolicitudId, response, response.FaenaCodigo, request.Motivo, ct); return response;
    }

    public async Task<ProcurementRequestResponse?> LinkPurchaseOrderAsync(string id, LinkPurchaseOrderRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user); Required(request.OcNumero, nameof(request.OcNumero)); Required(request.ProveedorRut, nameof(request.ProveedorRut)); Required(request.Reason, nameof(request.Reason));
        _db.ChangeTracker.Clear();
        var entity = await RequestQuery().SingleOrDefaultAsync(x => x.RequestNumber == NormalizeCode(id), ct); if (entity is null) return null;
        EnsureFaenaAccess(user, entity.Faena?.Code);
        var supplier = await _db.Suppliers.SingleOrDefaultAsync(x => x.TaxId == NormalizeCode(request.ProveedorRut), ct) ?? throw new DomainException($"El proveedor '{request.ProveedorRut}' no existe.");
        if (entity.PurchaseOrders.Any(x => x.PurchaseOrderNumber == NormalizeCode(request.OcNumero))) throw new DomainException("La OC ya esta asociada a la solicitud.");
        var order = new PurchaseOrderEntity { PurchaseOrderNumber = NormalizeCode(request.OcNumero)!, ProcurementRequestId = entity.Id, SupplierId = supplier.Id, OrderedAtUtc = request.FechaOC ?? DateTimeOffset.UtcNow, PromisedAtUtc = request.FechaComprometida, Cost = request.CostoOC, Currency = Text(request.Moneda) ?? entity.Lines.First().Currency, DocumentUrl = Text(request.DocumentoOcUrl), CreatedByUserId = user.UserId, Reason = request.Reason.Trim() };
        foreach (var line in entity.Lines) order.Lines.Add(new PurchaseOrderLineEntity { ProcurementRequestLineId = line.Id, Quantity = line.RequestedQuantity, UnitCost = request.CostoOC.HasValue ? request.CostoOC / line.RequestedQuantity : null });
        _db.PurchaseOrders.Add(order); entity.Status = (int)ProcurementRequestStatus.OCAsociada; entity.UpdatedAtUtc = DateTimeOffset.UtcNow; entity.UpdatedByUserId = user.UserId;
        await _db.SaveChangesAsync(ct);
        var response = ToResponse(await RequestQuery().SingleAsync(x => x.Id == entity.Id, ct)); await AuditAsync(user, "procurement.purchase_order_linked", "ProcurementRequest", response.SolicitudId, response, response.FaenaCodigo, request.Reason, ct); return response;
    }

    public async Task<ProcurementRequestResponse?> RegisterReceptionAsync(string id, RegisterProcurementReceptionRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user); Required(request.BodegaCodigo, nameof(request.BodegaCodigo)); Required(request.Reason, nameof(request.Reason)); EnsurePositive(request.CantidadRecibida);
        _db.ChangeTracker.Clear();
        var entity = await RequestQuery().SingleOrDefaultAsync(x => x.RequestNumber == NormalizeCode(id), ct); if (entity is null) return null;
        EnsureFaenaAccess(user, entity.Faena?.Code); var order = entity.PurchaseOrders.OrderByDescending(x => x.OrderedAtUtc).FirstOrDefault() ?? throw new DomainException("Debe asociar una OC antes de registrar recepcion.");
        var warehouse = await _db.Warehouses.SingleOrDefaultAsync(x => x.Code == NormalizeCode(request.BodegaCodigo), ct) ?? throw new DomainException($"La bodega '{request.BodegaCodigo}' no existe.");
        var line = entity.Lines.Single(); if (line.ReceivedQuantity + request.CantidadRecibida > line.RequestedQuantity) throw new DomainException("La recepcion no puede superar la cantidad solicitada.");
        string? receptionNumber = null; string? deliveryNumber = null;
        if (line.SparePart is not null)
        {
            var received = await _inventory.RegisterMovementAsync(new StockMovementRequest(StockMovementType.Reception, line.SparePart.Code, request.CantidadRecibida, $"Abastecimiento {entity.RequestNumber}: {request.Reason}", BodegaCodigo: warehouse.Code, ReferenceType: "Abastecimiento", ReferenceId: entity.RequestNumber), user, ct); receptionNumber = received.MovimientoId;
            if (request.DespachoDirectoOt) { var delivered = await _inventory.DeliverMaterialAsync(new DeliverMaterialRequest(line.SparePart.Code, warehouse.Code, request.CantidadRecibida, $"Despacho directo {entity.RequestNumber}: {request.Reason}", request.OtNumero ?? entity.WorkOrder?.WorkOrderNumber, request.ActivoCodigo ?? entity.Asset?.Code, request.FaenaCodigo ?? entity.Faena?.Code), user, ct); deliveryNumber = delivered.MovimientoId; }
        }
        else if (request.DespachoDirectoOt) throw new DomainException("El despacho directo requiere repuesto codificado.");
        var receipt = new ProcurementReceiptEntity { ProcurementRequestId = entity.Id, PurchaseOrderId = order.Id, WarehouseId = warehouse.Id, ReceivedAtUtc = request.FechaRecepcion ?? DateTimeOffset.UtcNow, DirectDispatchToWorkOrder = request.DespachoDirectoOt, ActualCost = request.CostoReal, ReceptionDocumentUrl = Text(request.DocumentoRecepcionUrl), DeliveryDocumentUrl = Text(request.DocumentoEntregaUrl), CreatedByUserId = user.UserId, Reason = request.Reason.Trim(), ReceptionMovementId = await MovementIdAsync(receptionNumber, ct), DeliveryMovementId = await MovementIdAsync(deliveryNumber, ct) };
        receipt.Lines.Add(new ProcurementReceiptLineEntity { ProcurementRequestLineId = line.Id, ReceivedQuantity = request.CantidadRecibida, DeliveredQuantity = request.DespachoDirectoOt ? request.CantidadRecibida : 0 }); _db.ProcurementReceipts.Add(receipt);
        line.ReceivedQuantity += request.CantidadRecibida; if (request.DespachoDirectoOt) line.DeliveredQuantity += request.CantidadRecibida; entity.WarehouseId = warehouse.Id; entity.Status = (int)ResolveStatus(line); entity.UpdatedAtUtc = DateTimeOffset.UtcNow; entity.UpdatedByUserId = user.UserId;
        await _db.SaveChangesAsync(ct); var response = ToResponse(await RequestQuery().SingleAsync(x => x.Id == entity.Id, ct)); await AuditAsync(user, "procurement.reception_registered", "ProcurementRequest", response.SolicitudId, response, response.FaenaCodigo, request.Reason, ct); return response;
    }

    public async Task<ProcurementRequestResponse?> RegisterDeliveryAsync(string id, DeliverProcurementRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanManage(user); Required(request.BodegaCodigo, nameof(request.BodegaCodigo)); Required(request.Reason, nameof(request.Reason)); EnsurePositive(request.CantidadEntregada);
        _db.ChangeTracker.Clear();
        var entity = await RequestQuery().SingleOrDefaultAsync(x => x.RequestNumber == NormalizeCode(id), ct); if (entity is null) return null;
        EnsureFaenaAccess(user, entity.Faena?.Code); var line = entity.Lines.Single(); if (line.SparePart is null) throw new DomainException("La entrega requiere repuesto codificado."); if (line.DeliveredQuantity + request.CantidadEntregada > line.ReceivedQuantity) throw new DomainException("La entrega no puede superar lo recibido.");
        var warehouse = await _db.Warehouses.SingleOrDefaultAsync(x => x.Code == NormalizeCode(request.BodegaCodigo), ct) ?? throw new DomainException($"La bodega '{request.BodegaCodigo}' no existe.");
        var movement = await _inventory.DeliverMaterialAsync(new DeliverMaterialRequest(line.SparePart.Code, warehouse.Code, request.CantidadEntregada, $"Entrega abastecimiento {entity.RequestNumber}: {request.Reason}", request.OtNumero ?? entity.WorkOrder?.WorkOrderNumber, request.ActivoCodigo ?? entity.Asset?.Code, request.FaenaCodigo ?? entity.Faena?.Code), user, ct);
        var receipt = new ProcurementReceiptEntity { ProcurementRequestId = entity.Id, PurchaseOrderId = entity.PurchaseOrders.OrderByDescending(x => x.OrderedAtUtc).FirstOrDefault()?.Id, WarehouseId = warehouse.Id, ReceivedAtUtc = request.FechaEntrega ?? DateTimeOffset.UtcNow, DeliveryDocumentUrl = Text(request.DocumentoEntregaUrl), CreatedByUserId = user.UserId, Reason = request.Reason.Trim(), DeliveryMovementId = await MovementIdAsync(movement.MovimientoId, ct) }; receipt.Lines.Add(new ProcurementReceiptLineEntity { ProcurementRequestLineId = line.Id, DeliveredQuantity = request.CantidadEntregada }); _db.ProcurementReceipts.Add(receipt);
        line.DeliveredQuantity += request.CantidadEntregada; entity.WarehouseId = warehouse.Id; entity.Status = (int)ResolveStatus(line); entity.UpdatedAtUtc = DateTimeOffset.UtcNow; entity.UpdatedByUserId = user.UserId;
        await _db.SaveChangesAsync(ct); var response = ToResponse(await RequestQuery().SingleAsync(x => x.Id == entity.Id, ct)); await AuditAsync(user, "procurement.delivery_registered", "ProcurementRequest", response.SolicitudId, response, response.FaenaCodigo, request.Reason, ct); return response;
    }

    private IQueryable<ProcurementRequestEntity> RequestQuery() => _db.ProcurementRequests.Include(x => x.Faena).Include(x => x.Warehouse).Include(x => x.Asset).Include(x => x.WorkOrder).Include(x => x.MaterialRequest).Include(x => x.Lines).ThenInclude(x => x.SparePart).Include(x => x.PurchaseOrders).ThenInclude(x => x.Supplier).Include(x => x.Receipts).ThenInclude(x => x.Lines);
    private static SupplierEntity Apply(UpsertSupplierRequest r, SupplierEntity e) { e.Name = r.Nombre.Trim(); e.Contact = Text(r.Contacto); e.Email = Text(r.Email); e.Phone = Text(r.Telefono); e.Address = Text(r.Direccion); e.ExpectedLeadTimeDays = r.LeadTimeEsperadoDias ?? 0; e.IsActive = r.Activo; e.Notes = Text(r.Observaciones); return e; }
    private static SupplierResponse ToResponse(SupplierEntity e) => new(e.TaxId, e.Name, e.Contact, e.Email, e.Phone, e.Address, e.ExpectedLeadTimeDays, e.IsActive, e.Notes);
    private static ProcurementRequestResponse ToResponse(ProcurementRequestEntity e)
    {
        var line = e.Lines.Single(); var order = e.PurchaseOrders.OrderByDescending(x => x.OrderedAtUtc).FirstOrDefault(); var receipts = e.Receipts.OrderBy(x => x.ReceivedAtUtc).ToArray(); var lastReceipt = receipts.LastOrDefault(); var actual = receipts.Select(x => x.ActualCost).LastOrDefault(x => x.HasValue); var status = (ProcurementRequestStatus)e.Status; var closed = status is ProcurementRequestStatus.Entregada or ProcurementRequestStatus.Cerrada or ProcurementRequestStatus.Cancelada; var overdue = order is not null && order.PromisedAtUtc.Date < DateTimeOffset.UtcNow.Date && !closed;
        return new(e.RequestNumber, status, e.MaterialRequest?.RequestNumber, line.ExternalRequestNumber, order?.PurchaseOrderNumber, order?.Supplier?.TaxId, order?.Supplier?.Name, line.SparePart?.Code, line.Description, line.RequestedQuantity, line.Unit, line.ReceivedQuantity, line.DeliveredQuantity, e.Faena?.Code, e.Warehouse?.Code, e.WorkOrder?.WorkOrderNumber, e.Asset?.Code, e.Reason, e.TechnicalRequestedAtUtc, e.MaintenanceApprovedAtUtc, e.SentToProcurementAtUtc, order?.OrderedAtUtc, order?.PromisedAtUtc, receipts.FirstOrDefault(x => x.Lines.Any(l => l.ReceivedQuantity > 0))?.ReceivedAtUtc, receipts.FirstOrDefault(x => x.Lines.Any(l => l.DeliveredQuantity > 0))?.ReceivedAtUtc, line.EstimatedCost, order?.Cost, actual, order?.Currency ?? line.Currency, line.SupportingDocumentUrl, order?.DocumentUrl, lastReceipt?.ReceptionDocumentUrl, lastReceipt?.DeliveryDocumentUrl, LeadTime(e.TechnicalRequestedAtUtc, e.MaintenanceApprovedAtUtc, e.SentToProcurementAtUtc, order?.OrderedAtUtc, receipts.FirstOrDefault(x => x.Lines.Any(l => l.ReceivedQuantity > 0))?.ReceivedAtUtc, receipts.FirstOrDefault(x => x.Lines.Any(l => l.DeliveredQuantity > 0))?.ReceivedAtUtc), overdue, e.CreatedByUserId, e.CreatedAtUtc, e.UpdatedByUserId, e.UpdatedAtUtc, line.Notes);
    }
    private static ProcurementRequestStatus ResolveStatus(ProcurementRequestLineEntity x) => x.DeliveredQuantity >= x.RequestedQuantity ? ProcurementRequestStatus.Entregada : x.ReceivedQuantity >= x.RequestedQuantity ? ProcurementRequestStatus.Recepcionada : ProcurementRequestStatus.RecepcionParcial;
    private static LeadTimeBreakdown LeadTime(DateTimeOffset requested, DateTimeOffset? approved, DateTimeOffset sent, DateTimeOffset? ordered, DateTimeOffset? received, DateTimeOffset? delivered) { var end = delivered ?? received ?? ordered ?? sent; return new(Days(requested, approved), Days(approved, sent), Days(sent, ordered), Days(ordered, received), Days(received, delivered), Days(requested, end)); }
    private static int? Days(DateTimeOffset? start, DateTimeOffset? end) => start.HasValue && end.HasValue ? (end.Value.Date - start.Value.Date).Days : null;
    private async Task<string> NextRequestNumberAsync(CancellationToken ct) { var numbers = await _db.ProcurementRequests.AsNoTracking().Select(x => x.RequestNumber).ToListAsync(ct); var next = numbers.Select(x => int.TryParse(x.Replace("AB-", "", StringComparison.OrdinalIgnoreCase), out var n) ? n : 0).DefaultIfEmpty().Max() + 1; return $"AB-{next:000000}"; }
    private async Task<Guid?> MovementIdAsync(string? number, CancellationToken ct) => string.IsNullOrWhiteSpace(number) ? null : await _db.StockMovements.Where(x => x.MovementNumber == number).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    private static async Task<T?> FindOptionalAsync<T>(DbSet<T> set, string? code, System.Linq.Expressions.Expression<Func<T, string>> codeSelector, string label, CancellationToken ct) where T : class { if (string.IsNullOrWhiteSpace(code)) return null; var value = NormalizeCode(code); var entity = await set.SingleOrDefaultAsync(System.Linq.Expressions.Expression.Lambda<Func<T,bool>>(System.Linq.Expressions.Expression.Equal(codeSelector.Body, System.Linq.Expressions.Expression.Constant(value)), codeSelector.Parameters), ct); return entity ?? throw new DomainException($"El {label} '{code}' no existe."); }
    private async Task AuditAsync(UserAccessContext user, string action, string entity, string id, object value, string? faena, string? reason, CancellationToken ct) => await _audit.RecordAsync(new(user.UserId, action, AuditModules.Procurement, entity, id, NewValue: JsonSerializer.Serialize(value), FaenaCodigo: faena, Severity: AuditSeverity.High, Reason: reason), ct);
    private static void ValidateSupplier(UpsertSupplierRequest x) { Required(x.Rut, nameof(x.Rut)); Required(x.Nombre, nameof(x.Nombre)); if (x.LeadTimeEsperadoDias.GetValueOrDefault() < 0) throw new DomainException("El lead time esperado no puede ser negativo."); }
    private static void Required(string? value, string field) { if (string.IsNullOrWhiteSpace(value)) throw new DomainException($"El campo {field} es obligatorio."); }
    private static void EnsurePositive(decimal value) { if (value <= 0) throw new DomainException("La cantidad debe ser mayor a cero."); }
    private static string? Text(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim(); private static string? NormalizeCode(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim().ToUpperInvariant();
    private static bool CanAccessFaena(UserAccessContext u, string? f) => string.IsNullOrWhiteSpace(f) || HasRole(u, AuthRoles.Admin, AuthRoles.Management, AuthRoles.Warehouse, AuthRoles.WarehouseSupervisor) || u.Permissions.Contains(AuthPermissions.ViewGlobalWarehouses, StringComparer.OrdinalIgnoreCase) || u.Faenas.Contains(f, StringComparer.OrdinalIgnoreCase);
    private static void EnsureFaenaAccess(UserAccessContext u, string? f) { if (!CanAccessFaena(u, f)) throw new UnauthorizedAccessException("No tiene acceso a la faena de la solicitud."); }
    private static void EnsureCanView(UserAccessContext u) { if (!HasRole(u, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Warehouse, AuthRoles.WarehouseSupervisor, AuthRoles.Management, AuthRoles.FaenaViewer)) throw new UnauthorizedAccessException("No tiene permisos para ver abastecimiento."); }
    private static void EnsureCanManage(UserAccessContext u) { if (!HasRole(u, AuthRoles.Admin, AuthRoles.WarehouseSupervisor) && !u.Permissions.Contains(AuthPermissions.AdjustStock, StringComparer.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("La gestion de abastecimiento requiere supervisor de bodega."); }
    private static bool HasRole(UserAccessContext u, params string[] roles) => roles.Any(r => u.Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
}
