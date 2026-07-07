using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Infrastructure.Inventory;

public sealed class InventoryService : IInventoryService
{
    private const string WarehousesSchema = "bodegas";
    private const string SparePartsSchema = "repuestos";
    private const string StockSchema = "stock_bodegas";
    private const string MovementsSchema = "stock_movements";
    private const string ReservationsSchema = "stock_reservations";
    private const string TransfersSchema = "stock_transfers";
    private const string FaenasSchema = "faenas";

    private readonly IDataProvider _dataProvider;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;

    public InventoryService(
        IDataProvider dataProvider,
        IAuditService auditService,
        IAuthorizationPolicyService authorizationPolicyService)
    {
        _dataProvider = dataProvider;
        _auditService = auditService;
        _authorizationPolicyService = authorizationPolicyService;
    }

    public async Task<InventoryDashboardResponse> GetDashboardAsync(
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        var spareParts = await BuildSparePartSummariesAsync(user, cancellationToken);
        var stock = await ListStockAsync(new StockQuery(), user, cancellationToken);
        var warehouses = await ListWarehousesAsync(new WarehouseQuery(), user, cancellationToken);
        var alerts = BuildAlerts(spareParts, stock);

        return new InventoryDashboardResponse(
            spareParts.Count,
            spareParts.Count(item => item.Critico),
            spareParts.Count(item => item.EsNoCodificado),
            spareParts.Count(item => item.BajoMinimo),
            spareParts.Count(item => item.CriticoSinStock),
            warehouses.Count,
            stock.Sum(item => item.StockFisico),
            stock.Sum(item => item.StockDisponible),
            alerts);
    }

    public async Task<IReadOnlyCollection<WarehouseResponse>> ListWarehousesAsync(
        WarehouseQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        return (await _dataProvider.ReadRowsAsync(WarehousesSchema, cancellationToken))
            .Select(ToWarehouse)
            .Where(item => query.IncludeInactive || item.Activa)
            .Where(item => !query.Tipo.HasValue || item.Tipo == query.Tipo)
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .OrderBy(item => item.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<WarehouseResponse> CreateWarehouseAsync(
        CreateWarehouseRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        ValidateRequired(request.Codigo, nameof(request.Codigo));
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        ValidateRequired(request.FaenaCodigo, nameof(request.FaenaCodigo));
        await EnsureFaenaExistsAsync(request.FaenaCodigo, cancellationToken);

        var rows = (await _dataProvider.ReadRowsAsync(WarehousesSchema, cancellationToken)).ToList();
        if (rows.Any(row => Same(row.GetValue("Codigo"), request.Codigo)))
        {
            throw new DomainException($"Ya existe una bodega con codigo '{request.Codigo}'.");
        }

        var rowToCreate = WarehouseRow(
            NormalizeCode(request.Codigo),
            request.Nombre,
            request.FaenaCodigo,
            request.Tipo,
            request.Ubicacion,
            request.UbicacionesInternas ?? [],
            request.Activa,
            request.Responsable,
            request.PermiteStockNegativo);

        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(WarehousesSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "warehouse.created", AuditModules.Warehouse, "Warehouse", request.Codigo, null, Serialize(rowToCreate), request.FaenaCodigo, null, cancellationToken);

        return ToWarehouse(rowToCreate);
    }

    public async Task<IReadOnlyCollection<SparePartSummary>> ListSparePartsAsync(
        SparePartQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        var rows = await BuildSparePartSummariesAsync(user, cancellationToken);
        return rows
            .Where(item => query.IncludeObsolete || item.Estado != SparePartStatus.Obsoleto)
            .Where(item => !query.Estado.HasValue || item.Estado == query.Estado)
            .Where(item => !query.CriticalOnly || item.Critico)
            .Where(item => !query.LowStockOnly || item.BajoMinimo || item.CriticoSinStock)
            .Where(item => string.IsNullOrWhiteSpace(query.Familia) || Same(item.FamiliaEquipo, query.Familia))
            .Where(item => MatchesSearch(item, query.Search))
            .OrderBy(item => item.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<SparePartDetail?> GetSparePartAsync(
        string code,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        var summary = (await BuildSparePartSummariesAsync(user, cancellationToken))
            .FirstOrDefault(item => Same(item.Codigo, code));
        if (summary is null)
        {
            return null;
        }

        var stock = await ListStockAsync(new StockQuery(RepuestoCodigo: summary.Codigo), user, cancellationToken);
        var movements = await ListMovementRowsAsync(summary.Codigo, cancellationToken);
        return new SparePartDetail(summary, stock, movements);
    }

    public async Task<SparePartDetail> CreateSparePartAsync(
        CreateSparePartRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        ValidateSparePartRequest(request.Descripcion, request.UnidadMedida, request.StockMinimo, request.StockMaximo, request.PuntoReposicion, request.LeadTimeEsperadoDias);

        var rows = (await _dataProvider.ReadRowsAsync(SparePartsSchema, cancellationToken)).ToList();
        EnsureUniqueSap(rows, request.CodigoSap, null);
        var code = NextSparePartCode(rows);
        var rowToCreate = SparePartRow(code, request);

        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(SparePartsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "spare_part.created", AuditModules.SpareParts, "SparePart", code, null, Serialize(rowToCreate), null, null, cancellationToken);

        return await GetSparePartAsync(code, user, cancellationToken) ??
               throw new InvalidOperationException("No fue posible leer el repuesto creado.");
    }

    public async Task<SparePartDetail?> UpdateSparePartAsync(
        string code,
        UpdateSparePartRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        ValidateSparePartRequest(request.Descripcion, request.UnidadMedida, request.StockMinimo, request.StockMaximo, request.PuntoReposicion, request.LeadTimeEsperadoDias);

        var rows = (await _dataProvider.ReadRowsAsync(SparePartsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("Codigo"), code));
        if (index < 0)
        {
            return null;
        }

        EnsureUniqueSap(rows, request.CodigoSap, code);
        var existing = rows[index];
        var updated = SparePartRow(existing.GetValue("Codigo") ?? NormalizeCode(code), request);

        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(SparePartsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "spare_part.updated", AuditModules.SpareParts, "SparePart", code, Serialize(existing), Serialize(updated), null, request.Reason, cancellationToken);

        return await GetSparePartAsync(code, user, cancellationToken);
    }

    public async Task<IReadOnlyCollection<StockItemResponse>> ListStockAsync(
        StockQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        var warehouses = (await _dataProvider.ReadRowsAsync(WarehousesSchema, cancellationToken))
            .Select(ToWarehouse)
            .ToDictionary(item => item.Codigo, StringComparer.OrdinalIgnoreCase);
        var spareParts = (await _dataProvider.ReadRowsAsync(SparePartsSchema, cancellationToken))
            .Select(row => ToSparePart(row, _authorizationPolicyService.CanViewCosts(user)))
            .ToDictionary(item => item.Codigo, StringComparer.OrdinalIgnoreCase);

        return (await _dataProvider.ReadRowsAsync(StockSchema, cancellationToken))
            .Select(row => ToStock(row, warehouses, spareParts))
            .Where(item => !string.IsNullOrWhiteSpace(item.BodegaCodigo) && !string.IsNullOrWhiteSpace(item.RepuestoCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.BodegaCodigo) || Same(item.BodegaCodigo, query.BodegaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.RepuestoCodigo) || Same(item.RepuestoCodigo, query.RepuestoCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => !query.LowStockOnly || item.BajoMinimo || item.CriticoSinStock)
            .Where(item => !query.CriticalOnly || item.RepuestoCritico)
            .OrderBy(item => item.BodegaCodigo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RepuestoCodigo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<StockMovementResponse>> ListMovementsAsync(
        StockMovementQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        var take = query.Take <= 0 ? 100 : Math.Min(query.Take, 500);
        return (await _dataProvider.ReadRowsAsync(MovementsSchema, cancellationToken))
            .Select(ToMovement)
            .Where(item => string.IsNullOrWhiteSpace(query.BodegaCodigo) || Same(item.BodegaCodigo, query.BodegaCodigo) || Same(item.BodegaOrigenCodigo, query.BodegaCodigo) || Same(item.BodegaDestinoCodigo, query.BodegaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.RepuestoCodigo) || Same(item.RepuestoCodigo, query.RepuestoCodigo))
            .Where(item => !query.Type.HasValue || item.Type == query.Type)
            .Where(item => string.IsNullOrWhiteSpace(query.ReferenceType) || Same(item.ReferenceType, query.ReferenceType))
            .Where(item => string.IsNullOrWhiteSpace(query.ReferenceId) || Same(item.ReferenceId, query.ReferenceId))
            .OrderByDescending(item => item.FechaUtc)
            .Take(take)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<StockReservationResponse>> ListReservationsAsync(
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        return (await _dataProvider.ReadRowsAsync(ReservationsSchema, cancellationToken))
            .Select(ToReservation)
            .OrderByDescending(item => item.FechaUtc)
            .ToArray();
    }

    public async Task<StockReservationResponse> CreateReservationAsync(
        CreateStockReservationRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanAdjustStock(user);
        ValidateRequired(request.WorkOrderId, nameof(request.WorkOrderId));
        ValidateRequired(request.RequestedBy, nameof(request.RequestedBy));

        var reservationId = $"RES-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        await RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.Reservation,
            request.RepuestoCodigo,
            request.Quantity,
            request.Reason,
            BodegaCodigo: request.BodegaCodigo,
            ReferenceType: "ReservaOT",
            ReferenceId: reservationId), user, cancellationToken);

        var rows = (await _dataProvider.ReadRowsAsync(ReservationsSchema, cancellationToken)).ToList();
        var row = ReservationRow(
            reservationId,
            StockReservationStatus.Activa,
            DateTimeOffset.UtcNow,
            request.RepuestoCodigo,
            request.BodegaCodigo,
            request.Quantity,
            0,
            0,
            request.WorkOrderId,
            request.RequestedBy,
            request.Reason,
            user.UserId);
        rows.Add(row);
        await _dataProvider.SaveRowsAsync(ReservationsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "stock.reservation_created", AuditModules.Stock, "StockReservation", reservationId, null, Serialize(row), null, request.Reason, cancellationToken, AuditSeverity.High);

        return ToReservation(row);
    }

    public async Task<StockReservationResponse?> ReleaseReservationAsync(
        string reservationId,
        ReleaseStockReservationRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanAdjustStock(user);
        ValidateRequired(reservationId, nameof(reservationId));
        ValidateRequired(request.Reason, nameof(request.Reason));
        EnsurePositive(request.Quantity);

        var rows = (await _dataProvider.ReadRowsAsync(ReservationsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("ReservaId"), reservationId));
        if (index < 0)
        {
            return null;
        }

        var current = ToReservation(rows[index]);
        if (current.Estado is StockReservationStatus.Entregada or StockReservationStatus.Liberada or StockReservationStatus.Cancelada)
        {
            throw new DomainException("La reserva no admite nuevas liberaciones.");
        }

        if (request.Quantity > current.CantidadPendiente)
        {
            throw new DomainException("La cantidad a liberar excede el saldo pendiente de la reserva.");
        }

        await RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.ReservationRelease,
            current.RepuestoCodigo,
            request.Quantity,
            request.Reason,
            BodegaCodigo: current.BodegaCodigo,
            ReferenceType: "ReservaOT",
            ReferenceId: current.ReservaId), user, cancellationToken);

        var released = current.CantidadLiberada + request.Quantity;
        var pending = current.CantidadReservada - current.CantidadEntregada - released;
        var nextStatus = pending <= 0 ? StockReservationStatus.Liberada : current.Estado;
        var previous = rows[index];
        var updated = ReservationRow(
            current.ReservaId,
            nextStatus,
            current.FechaUtc,
            current.RepuestoCodigo,
            current.BodegaCodigo,
            current.CantidadReservada,
            current.CantidadEntregada,
            released,
            current.WorkOrderId,
            current.Solicitante,
            current.Motivo,
            current.UsuarioId);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(ReservationsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "stock.reservation_released", AuditModules.Stock, "StockReservation", current.ReservaId, Serialize(previous), Serialize(updated), null, request.Reason, cancellationToken, AuditSeverity.High);

        return ToReservation(updated);
    }

    public async Task<StockMovementResponse> DeliverMaterialAsync(
        DeliverMaterialRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanAdjustStock(user);
        ValidateRequired(request.Reason, nameof(request.Reason));
        if (string.IsNullOrWhiteSpace(request.WorkOrderId) &&
            string.IsNullOrWhiteSpace(request.AssetCode) &&
            string.IsNullOrWhiteSpace(request.FaenaCodigo) &&
            string.IsNullOrWhiteSpace(request.CostCenter))
        {
            throw new DomainException("La entrega de material requiere OT, activo, faena o centro de costo.");
        }

        var referenceType = BuildConsumptionReferenceType(request);
        var referenceId = request.WorkOrderId ?? request.AssetCode ?? request.FaenaCodigo ?? request.CostCenter;

        if (!string.IsNullOrWhiteSpace(request.ReservationId))
        {
            await MarkReservationDeliveredAsync(request.ReservationId, request.Quantity, request.Reason, user, cancellationToken);
        }

        return await RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.MaintenanceConsumption,
            request.RepuestoCodigo,
            request.Quantity,
            request.Reason,
            BodegaCodigo: request.BodegaCodigo,
            ReferenceType: referenceType,
            ReferenceId: referenceId), user, cancellationToken);
    }

    public async Task<IReadOnlyCollection<StockTransferResponse>> ListTransfersAsync(
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanViewWarehouses(user);
        var movements = await ListMovementsAsync(new StockMovementQuery(ReferenceType: "Transferencia", Take: 500), user, cancellationToken);
        return (await _dataProvider.ReadRowsAsync(TransfersSchema, cancellationToken))
            .Select(row => ToTransfer(row, movements.Where(item => Same(item.ReferenceId, row.GetValue("TransferenciaId"))).ToArray()))
            .OrderByDescending(item => item.FechaSolicitudUtc)
            .ToArray();
    }

    public async Task<StockTransferResponse> TransferStockAsync(
        TransferStockRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanAdjustStock(user);
        ValidateRequired(request.SourceWarehouseCode, nameof(request.SourceWarehouseCode));
        ValidateRequired(request.TransitWarehouseCode, nameof(request.TransitWarehouseCode));
        ValidateRequired(request.TargetWarehouseCode, nameof(request.TargetWarehouseCode));
        if (Same(request.SourceWarehouseCode, request.TargetWarehouseCode))
        {
            throw new DomainException("La transferencia requiere bodegas origen y destino distintas.");
        }

        await EnsureWarehouseIsTransitAsync(request.TransitWarehouseCode, cancellationToken);
        var transferId = EmptyToNull(request.TransferId) ?? $"TRF-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var rows = (await _dataProvider.ReadRowsAsync(TransfersSchema, cancellationToken)).ToList();
        if (rows.Any(row => Same(row.GetValue("TransferenciaId"), transferId)))
        {
            throw new DomainException($"Ya existe la transferencia '{transferId}'.");
        }

        await RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.TransferOut,
            request.RepuestoCodigo,
            request.Quantity,
            request.Reason,
            BodegaCodigo: request.SourceWarehouseCode,
            SourceWarehouseCode: request.SourceWarehouseCode,
            TargetWarehouseCode: request.TransitWarehouseCode,
            ReferenceType: "Transferencia",
            ReferenceId: transferId), user, cancellationToken);

        var row = TransferRow(
            transferId,
            StockTransferStatus.EnTransito,
            DateTimeOffset.UtcNow,
            null,
            request.RepuestoCodigo,
            request.SourceWarehouseCode,
            request.TransitWarehouseCode,
            request.TargetWarehouseCode,
            request.Quantity,
            request.Reason,
            user.UserId,
            null,
            null);
        rows.Add(row);
        await _dataProvider.SaveRowsAsync(TransfersSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "stock.transfer_created", AuditModules.Stock, "StockTransfer", transferId, null, Serialize(row), null, request.Reason, cancellationToken, AuditSeverity.High);

        var movements = await ListMovementsAsync(new StockMovementQuery(ReferenceType: "Transferencia", ReferenceId: transferId), user, cancellationToken);
        return ToTransfer(row, movements);
    }

    public async Task<StockTransferResponse?> ReceiveTransferAsync(
        string transferId,
        ReceiveTransferRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanAdjustStock(user);
        ValidateRequired(transferId, nameof(transferId));
        ValidateRequired(request.Reason, nameof(request.Reason));

        var rows = (await _dataProvider.ReadRowsAsync(TransfersSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("TransferenciaId"), transferId));
        if (index < 0)
        {
            return null;
        }

        var current = ToTransfer(rows[index], []);
        if (current.Estado != StockTransferStatus.EnTransito)
        {
            throw new DomainException("La transferencia no esta en transito.");
        }

        await RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.TransferReception,
            current.RepuestoCodigo,
            current.Cantidad,
            request.Reason,
            BodegaCodigo: current.BodegaDestinoCodigo,
            SourceWarehouseCode: current.BodegaTransitoCodigo,
            TargetWarehouseCode: current.BodegaDestinoCodigo,
            ReferenceType: "Transferencia",
            ReferenceId: current.TransferenciaId), user, cancellationToken);

        var updated = TransferRow(
            current.TransferenciaId,
            StockTransferStatus.Recibida,
            current.FechaSolicitudUtc,
            DateTimeOffset.UtcNow,
            current.RepuestoCodigo,
            current.BodegaOrigenCodigo,
            current.BodegaTransitoCodigo,
            current.BodegaDestinoCodigo,
            current.Cantidad,
            current.Motivo,
            current.UsuarioId,
            user.UserId,
            request.Reason);
        var previous = rows[index];
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(TransfersSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "stock.transfer_received", AuditModules.Stock, "StockTransfer", current.TransferenciaId, Serialize(previous), Serialize(updated), null, request.Reason, cancellationToken, AuditSeverity.High);

        var movements = await ListMovementsAsync(new StockMovementQuery(ReferenceType: "Transferencia", ReferenceId: current.TransferenciaId), user, cancellationToken);
        return ToTransfer(updated, movements);
    }

    public async Task<StockMovementResponse> ReturnStockAsync(
        ReturnStockRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanAdjustStock(user);
        ValidateRequired(request.Reason, nameof(request.Reason));
        if (string.IsNullOrWhiteSpace(request.WorkOrderId) && string.IsNullOrWhiteSpace(request.AssetCode))
        {
            throw new DomainException("La devolucion requiere OT o activo de origen.");
        }

        return await RegisterMovementAsync(new StockMovementRequest(
            request.Reusable ? StockMovementType.ReturnFromWorkOrder : StockMovementType.MaterialWriteOff,
            request.RepuestoCodigo,
            request.Quantity,
            request.Reason,
            BodegaCodigo: request.BodegaCodigo,
            ReferenceType: request.Reusable ? "DevolucionReutilizable" : "DevolucionNoReutilizable",
            ReferenceId: request.WorkOrderId ?? request.AssetCode), user, cancellationToken);
    }

    public async Task<StockMovementResponse> AdjustStockAsync(
        AdjustStockRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanAdjustStock(user);
        ValidateRequired(request.Reason, nameof(request.Reason));
        if (request.Quantity == 0)
        {
            throw new DomainException("La cantidad de ajuste debe ser distinta de cero.");
        }

        if (request.RequiresSupervisorApproval)
        {
            ValidateRequired(request.SupervisorApprovalUserId, nameof(request.SupervisorApprovalUserId));
        }

        var type = request.Quantity > 0 ? StockMovementType.PositiveAdjustment : StockMovementType.NegativeAdjustment;
        var referenceType = request.RequiresSupervisorApproval ? "AjusteSupervisor" : "AjusteInventario";
        return await RegisterMovementAsync(new StockMovementRequest(
            type,
            request.RepuestoCodigo,
            Math.Abs(request.Quantity),
            request.Reason,
            BodegaCodigo: request.BodegaCodigo,
            ReferenceType: referenceType,
            ReferenceId: request.SupervisorApprovalUserId,
            AllowNegativeException: request.AllowNegativeException), user, cancellationToken);
    }

    public async Task<StockMovementResponse> WriteOffStockAsync(
        WriteOffStockRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanAdjustStock(user);
        ValidateRequired(request.Reason, nameof(request.Reason));
        return await RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.MaterialWriteOff,
            request.RepuestoCodigo,
            request.Quantity,
            request.Reason,
            BodegaCodigo: request.BodegaCodigo,
            ReferenceType: request.ReferenceType ?? "BajaMaterial",
            ReferenceId: request.ReferenceId,
            AllowNegativeException: request.AllowNegativeException), user, cancellationToken);
    }

    public async Task<StockMovementResponse> RegisterMovementAsync(
        StockMovementRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanAdjustStock(user);
        ValidateRequired(request.RepuestoCodigo, nameof(request.RepuestoCodigo));
        ValidateRequired(request.Reason, nameof(request.Reason));
        if (request.Quantity == 0)
        {
            throw new DomainException("La cantidad del movimiento debe ser distinta de cero.");
        }

        if (request.Type == StockMovementType.MaintenanceConsumption && string.IsNullOrWhiteSpace(request.ReferenceId))
        {
            throw new DomainException("El consumo de mantenimiento requiere OT, activo, faena o centro de costo.");
        }

        var spareRows = await _dataProvider.ReadRowsAsync(SparePartsSchema, cancellationToken);
        var spare = spareRows.FirstOrDefault(row => Same(row.GetValue("Codigo"), request.RepuestoCodigo));
        if (spare is null)
        {
            throw new DomainException($"El repuesto '{request.RepuestoCodigo}' no existe.");
        }

        var warehouseRows = (await _dataProvider.ReadRowsAsync(WarehousesSchema, cancellationToken)).ToList();
        var stockRows = (await _dataProvider.ReadRowsAsync(StockSchema, cancellationToken)).ToList();
        var movementRows = (await _dataProvider.ReadRowsAsync(MovementsSchema, cancellationToken)).ToList();

        var primaryWarehouse = ResolvePrimaryWarehouseCode(request);
        var primaryResult = ApplyMovementToWarehouse(request, primaryWarehouse, stockRows, warehouseRows, spare, user);
        var movementDate = DateTimeOffset.UtcNow;
        var movement = MovementRow(
            Guid.NewGuid().ToString("D"),
            movementDate,
            request,
            primaryWarehouse,
            primaryResult,
            user.UserId);
        var appliedMovements = new List<DataRow> { movement };

        if (IsTransferPairMovement(request.Type) &&
            !string.IsNullOrWhiteSpace(request.SourceWarehouseCode) &&
            !string.IsNullOrWhiteSpace(request.TargetWarehouseCode))
        {
            var primaryIsSource = primaryWarehouse.Equals(request.SourceWarehouseCode, StringComparison.OrdinalIgnoreCase);
            var secondaryWarehouse = primaryIsSource ? request.TargetWarehouseCode : request.SourceWarehouseCode;
            var secondaryType = ResolveSecondaryTransferType(request, primaryWarehouse, warehouseRows);
            var secondaryRequest = request with
            {
                Type = secondaryType,
                BodegaCodigo = secondaryWarehouse
            };
            var secondaryResult = ApplyMovementToWarehouse(secondaryRequest, secondaryWarehouse!, stockRows, warehouseRows, spare, user);
            appliedMovements.Add(MovementRow(
                Guid.NewGuid().ToString("D"),
                movementDate,
                secondaryRequest,
                secondaryWarehouse!,
                secondaryResult,
                user.UserId));
        }

        await _dataProvider.SaveRowsAsync(StockSchema, stockRows, cancellationToken);

        movementRows.AddRange(appliedMovements);
        await _dataProvider.SaveRowsAsync(MovementsSchema, movementRows, cancellationToken);

        foreach (var appliedMovement in appliedMovements)
        {
            await RecordAuditAsync(
                user,
                "stock.movement_registered",
                AuditModules.Stock,
                "StockMovement",
                appliedMovement.GetValue("MovimientoId") ?? string.Empty,
                null,
                Serialize(appliedMovement),
                ResolveFaena(warehouseRows, appliedMovement.GetValue("BodegaCodigo") ?? primaryWarehouse),
                request.Reason,
                cancellationToken,
                request.AllowNegativeException ? AuditSeverity.Critical : AuditSeverity.High);
        }

        return ToMovement(movement);
    }

    private async Task<IReadOnlyCollection<SparePartSummary>> BuildSparePartSummariesAsync(
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var canViewCosts = _authorizationPolicyService.CanViewCosts(user);
        var stock = await ListStockAsync(new StockQuery(), user, cancellationToken);

        return (await _dataProvider.ReadRowsAsync(SparePartsSchema, cancellationToken))
            .Select(row => ToSparePart(row, canViewCosts))
            .Select(part =>
            {
                var partStock = stock.Where(item => Same(item.RepuestoCodigo, part.Codigo)).ToArray();
                var physical = partStock.Sum(item => item.StockFisico);
                var reserved = partStock.Sum(item => item.StockReservado);
                var available = physical - reserved;
                var belowMinimum = partStock.Any(item => item.BajoMinimo) ||
                                   (part.StockMinimo > 0 && available < part.StockMinimo);
                var criticalNoStock = part.Critico && available <= 0;

                return part with
                {
                    StockFisicoTotal = physical,
                    StockReservadoTotal = reserved,
                    StockDisponibleTotal = available,
                    BajoMinimo = belowMinimum,
                    CriticoSinStock = criticalNoStock
                };
            })
            .ToArray();
    }

    private StockMovementApplicationResult ApplyMovementToWarehouse(
        StockMovementRequest request,
        string warehouseCode,
        List<DataRow> stockRows,
        IReadOnlyCollection<DataRow> warehouseRows,
        DataRow spare,
        UserAccessContext user)
    {
        ValidateRequired(warehouseCode, nameof(warehouseCode));
        var warehouse = warehouseRows.FirstOrDefault(row => Same(row.GetValue("Codigo"), warehouseCode));
        if (warehouse is null)
        {
            throw new DomainException($"La bodega '{warehouseCode}' no existe.");
        }

        var index = stockRows.FindIndex(row =>
            Same(row.GetValue("BodegaCodigo"), warehouseCode) &&
            Same(row.GetValue("RepuestoCodigo"), request.RepuestoCodigo));
        var stockRow = index >= 0
            ? stockRows[index]
            : StockRow(
                warehouseCode,
                NormalizeCode(request.RepuestoCodigo),
                0,
                0,
                ParseDecimal(spare.GetValue("StockMinimo")),
                ParseDecimal(spare.GetValue("StockMaximo")),
                ParseDecimal(spare.GetValue("PuntoReposicion")),
                DateTimeOffset.UtcNow);

        var previousPhysical = ParseDecimal(stockRow.GetValue("StockFisico"));
        var previousReserved = ParseDecimal(stockRow.GetValue("StockReservado"));
        var nextPhysical = previousPhysical;
        var nextReserved = previousReserved;
        var quantity = request.Quantity;

        switch (request.Type)
        {
            case StockMovementType.Reception:
            case StockMovementType.TransferIn:
            case StockMovementType.InTransit:
            case StockMovementType.TransferReception:
            case StockMovementType.ReturnFromWorkOrder:
            case StockMovementType.PositiveAdjustment:
                EnsurePositive(quantity);
                nextPhysical += quantity;
                break;
            case StockMovementType.MaintenanceConsumption:
            case StockMovementType.TransferOut:
            case StockMovementType.NegativeAdjustment:
                EnsurePositive(quantity);
                nextPhysical -= quantity;
                break;
            case StockMovementType.MaterialWriteOff:
                EnsurePositive(quantity);
                if (!Same(request.ReferenceType, "DevolucionNoReutilizable"))
                {
                    nextPhysical -= quantity;
                }

                break;
            case StockMovementType.Reservation:
                EnsurePositive(quantity);
                nextReserved += quantity;
                break;
            case StockMovementType.ReservationRelease:
                EnsurePositive(quantity);
                nextReserved = Math.Max(0, nextReserved - quantity);
                break;
            case StockMovementType.Adjustment:
                nextPhysical += quantity;
                break;
            case StockMovementType.CountCorrection:
                nextPhysical = quantity;
                break;
            default:
                throw new DomainException("Tipo de movimiento de stock no soportado.");
        }

        ValidateStockResult(nextPhysical, nextReserved, request.AllowNegativeException, user, warehouse);

        var updated = StockRow(
            warehouseCode,
            NormalizeCode(request.RepuestoCodigo),
            nextPhysical,
            nextReserved,
            ParseDecimal(stockRow.GetValue("StockMinimo"), ParseDecimal(spare.GetValue("StockMinimo"))),
            ParseDecimal(stockRow.GetValue("StockMaximo"), ParseDecimal(spare.GetValue("StockMaximo"))),
            ParseDecimal(stockRow.GetValue("PuntoReposicion"), ParseDecimal(spare.GetValue("PuntoReposicion"))),
            DateTimeOffset.UtcNow);

        if (index >= 0)
        {
            stockRows[index] = updated;
        }
        else
        {
            stockRows.Add(updated);
        }

        return new StockMovementApplicationResult(previousPhysical, nextPhysical, previousReserved, nextReserved);
    }

    private void ValidateStockResult(
        decimal nextPhysical,
        decimal nextReserved,
        bool allowNegativeException,
        UserAccessContext user,
        DataRow warehouse)
    {
        var available = nextPhysical - nextReserved;
        if (nextReserved < 0)
        {
            throw new DomainException("El stock reservado no puede quedar negativo.");
        }

        if (nextPhysical >= 0 && available >= 0)
        {
            return;
        }

        var warehouseAllowsNegative = ParseBool(warehouse.GetValue("PermiteStockNegativo"));
        if (!allowNegativeException || !warehouseAllowsNegative || !_authorizationPolicyService.CanAdjustStock(user))
        {
            throw new DomainException("El movimiento dejaria stock negativo. Se requiere configuracion excepcional auditada.");
        }
    }

    private async Task MarkReservationDeliveredAsync(
        string reservationId,
        decimal quantity,
        string reason,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsurePositive(quantity);
        var rows = (await _dataProvider.ReadRowsAsync(ReservationsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("ReservaId"), reservationId));
        if (index < 0)
        {
            throw new DomainException($"La reserva '{reservationId}' no existe.");
        }

        var current = ToReservation(rows[index]);
        if (current.Estado is StockReservationStatus.Entregada or StockReservationStatus.Liberada or StockReservationStatus.Cancelada)
        {
            throw new DomainException("La reserva no admite entrega de material.");
        }

        if (quantity > current.CantidadPendiente)
        {
            throw new DomainException("La entrega excede el saldo pendiente de la reserva.");
        }

        await RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.ReservationRelease,
            current.RepuestoCodigo,
            quantity,
            reason,
            BodegaCodigo: current.BodegaCodigo,
            ReferenceType: "ReservaOT",
            ReferenceId: current.ReservaId), user, cancellationToken);

        var delivered = current.CantidadEntregada + quantity;
        var pending = current.CantidadReservada - delivered - current.CantidadLiberada;
        var status = pending <= 0 ? StockReservationStatus.Entregada : StockReservationStatus.ParcialmenteEntregada;
        var previous = rows[index];
        var updated = ReservationRow(
            current.ReservaId,
            status,
            current.FechaUtc,
            current.RepuestoCodigo,
            current.BodegaCodigo,
            current.CantidadReservada,
            delivered,
            current.CantidadLiberada,
            current.WorkOrderId,
            current.Solicitante,
            current.Motivo,
            current.UsuarioId);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(ReservationsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "stock.reservation_delivered", AuditModules.Stock, "StockReservation", current.ReservaId, Serialize(previous), Serialize(updated), null, reason, cancellationToken, AuditSeverity.High);
    }

    private async Task EnsureWarehouseIsTransitAsync(string warehouseCode, CancellationToken cancellationToken)
    {
        var warehouse = (await _dataProvider.ReadRowsAsync(WarehousesSchema, cancellationToken))
            .FirstOrDefault(row => Same(row.GetValue("Codigo"), warehouseCode));
        if (warehouse is null)
        {
            throw new DomainException($"La bodega de transito '{warehouseCode}' no existe.");
        }

        if (ParseEnum(warehouse.GetValue("TipoBodega"), WarehouseType.Faena) != WarehouseType.Transito)
        {
            throw new DomainException("La transferencia requiere una bodega tipo Transito.");
        }
    }

    private async Task<IReadOnlyCollection<StockMovementResponse>> ListMovementRowsAsync(
        string repuestoCodigo,
        CancellationToken cancellationToken)
    {
        return (await _dataProvider.ReadRowsAsync(MovementsSchema, cancellationToken))
            .Select(ToMovement)
            .Where(item => Same(item.RepuestoCodigo, repuestoCodigo))
            .OrderByDescending(item => item.FechaUtc)
            .Take(50)
            .ToArray();
    }

    private async Task EnsureFaenaExistsAsync(string faenaCodigo, CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(FaenasSchema, cancellationToken);
        if (!rows.Any(row => Same(row.GetValue("Codigo"), faenaCodigo)))
        {
            throw new DomainException($"La faena '{faenaCodigo}' no existe.");
        }
    }

    private void EnsureCanViewWarehouses(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanViewWarehouses(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso al modulo de bodega.");
        }
    }

    private void EnsureCanAdjustStock(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanAdjustStock(user))
        {
            throw new UnauthorizedAccessException("Registrar movimientos de stock requiere permiso de ajuste.");
        }
    }

    private static IReadOnlyCollection<StockAlertResponse> BuildAlerts(
        IReadOnlyCollection<SparePartSummary> spareParts,
        IReadOnlyCollection<StockItemResponse> stock)
    {
        var alerts = new List<StockAlertResponse>();
        alerts.AddRange(stock
            .Where(item => item.BajoMinimo)
            .Select(item => new StockAlertResponse(
                $"LOW-{item.BodegaCodigo}-{item.RepuestoCodigo}",
                "Warning",
                item.RepuestoCodigo,
                item.RepuestoDescripcion,
                item.BodegaCodigo,
                $"Stock bajo minimo en {item.BodegaCodigo}: disponible {FormatDecimal(item.StockDisponible)} / minimo {FormatDecimal(item.StockMinimo)}.")));

        alerts.AddRange(spareParts
            .Where(item => item.CriticoSinStock)
            .Select(item => new StockAlertResponse(
                $"CRITICAL-{item.Codigo}",
                "Critical",
                item.Codigo,
                item.Descripcion,
                null,
                "Repuesto critico sin stock disponible.")));

        return alerts
            .GroupBy(item => item.AlertKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.Severity, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToArray();
    }

    private static StockItemResponse ToStock(
        DataRow row,
        IReadOnlyDictionary<string, WarehouseResponse> warehouses,
        IReadOnlyDictionary<string, SparePartSummary> spareParts)
    {
        var warehouseCode = row.GetValue("BodegaCodigo")?.Trim() ?? string.Empty;
        var sparePartCode = row.GetValue("RepuestoCodigo")?.Trim() ?? string.Empty;
        warehouses.TryGetValue(warehouseCode, out var warehouse);
        spareParts.TryGetValue(sparePartCode, out var sparePart);

        var physical = ParseDecimal(row.GetValue("StockFisico"));
        var reserved = ParseDecimal(row.GetValue("StockReservado"));
        var available = physical - reserved;
        var minimum = ParseDecimal(row.GetValue("StockMinimo"), sparePart?.StockMinimo ?? 0);
        var maximum = ParseDecimal(row.GetValue("StockMaximo"), sparePart?.StockMaximo ?? 0);
        var reorderPoint = ParseDecimal(row.GetValue("PuntoReposicion"), sparePart?.PuntoReposicion ?? 0);
        var isCritical = sparePart?.Critico ?? false;

        return new StockItemResponse(
            warehouseCode,
            warehouse?.Nombre ?? warehouseCode,
            warehouse?.FaenaCodigo ?? string.Empty,
            sparePartCode,
            sparePart?.Descripcion ?? sparePartCode,
            sparePart?.UnidadMedida ?? string.Empty,
            isCritical,
            physical,
            reserved,
            available,
            minimum,
            maximum,
            reorderPoint,
            minimum > 0 && available < minimum,
            isCritical && available <= 0,
            ParseDate(row.GetValue("ActualizadoEnUtc")));
    }

    private static WarehouseResponse ToWarehouse(DataRow row)
    {
        return new WarehouseResponse(
            row.GetValue("Codigo")?.Trim() ?? string.Empty,
            row.GetValue("Nombre")?.Trim() ?? string.Empty,
            row.GetValue("FaenaCodigo")?.Trim() ?? string.Empty,
            ParseEnum(row.GetValue("TipoBodega"), WarehouseType.Faena),
            EmptyToNull(row.GetValue("Ubicacion")),
            SplitList(row.GetValue("UbicacionesInternas")),
            ParseBool(row.GetValue("Activa"), true),
            EmptyToNull(row.GetValue("Responsable")),
            ParseBool(row.GetValue("PermiteStockNegativo")));
    }

    private SparePartSummary ToSparePart(DataRow row, bool canViewCosts)
    {
        var sap = EmptyToNull(row.GetValue("CodigoSap"));
        var family = EmptyToNull(row.GetValue("FamiliaEquipo")) ?? EmptyToNull(row.GetValue("Familia"));
        return new SparePartSummary(
            row.GetValue("Codigo")?.Trim() ?? string.Empty,
            sap,
            EmptyToNull(row.GetValue("CodigoProveedor")),
            row.GetValue("Descripcion")?.Trim() ?? string.Empty,
            row.GetValue("DescripcionTecnica")?.Trim() ?? row.GetValue("Descripcion")?.Trim() ?? string.Empty,
            row.GetValue("UnidadMedida")?.Trim() ?? string.Empty,
            family,
            EmptyToNull(row.GetValue("MarcaFabricante")) ?? EmptyToNull(row.GetValue("Marca")),
            EmptyToNull(row.GetValue("ModeloReferencia")) ?? EmptyToNull(row.GetValue("Modelo")),
            ParseBool(row.GetValue("Critico")),
            ParseDecimal(row.GetValue("StockMinimo")),
            ParseDecimal(row.GetValue("StockMaximo")),
            ParseDecimal(row.GetValue("PuntoReposicion")),
            ParseInt(row.GetValue("LeadTimeEsperadoDias")),
            canViewCosts ? ParseDecimal(row.GetValue("CostoUnitarioPromedio")) : null,
            ParseEnum(row.GetValue("Estado"), SparePartStatus.Activo),
            ParseBool(row.GetValue("EsNoCodificado"), string.IsNullOrWhiteSpace(sap)),
            EmptyToNull(row.GetValue("ProveedorPreferente")),
            EmptyToNull(row.GetValue("ReemplazoCodigo")),
            0,
            0,
            0,
            false,
            false);
    }

    private static StockMovementResponse ToMovement(DataRow row)
    {
        return new StockMovementResponse(
            row.GetValue("MovimientoId")?.Trim() ?? string.Empty,
            ParseDate(row.GetValue("FechaUtc")) ?? DateTimeOffset.MinValue,
            ParseEnum(row.GetValue("Tipo"), StockMovementType.Adjustment),
            row.GetValue("RepuestoCodigo")?.Trim() ?? string.Empty,
            EmptyToNull(row.GetValue("BodegaCodigo")),
            EmptyToNull(row.GetValue("BodegaOrigenCodigo")),
            EmptyToNull(row.GetValue("BodegaDestinoCodigo")),
            ParseDecimal(row.GetValue("Cantidad")),
            ParseDecimal(row.GetValue("StockFisicoAnterior")),
            ParseDecimal(row.GetValue("StockFisicoNuevo")),
            ParseDecimal(row.GetValue("StockReservadoAnterior")),
            ParseDecimal(row.GetValue("StockReservadoNuevo")),
            row.GetValue("Motivo")?.Trim() ?? string.Empty,
            row.GetValue("UsuarioId")?.Trim() ?? string.Empty,
            EmptyToNull(row.GetValue("ReferenciaTipo")),
            EmptyToNull(row.GetValue("ReferenciaId")),
            ParseBool(row.GetValue("PermiteNegativoExcepcional")));
    }

    private static StockReservationResponse ToReservation(DataRow row)
    {
        var reserved = ParseDecimal(row.GetValue("CantidadReservada"));
        var delivered = ParseDecimal(row.GetValue("CantidadEntregada"));
        var released = ParseDecimal(row.GetValue("CantidadLiberada"));
        return new StockReservationResponse(
            row.GetValue("ReservaId")?.Trim() ?? string.Empty,
            ParseEnum(row.GetValue("Estado"), StockReservationStatus.Activa),
            ParseDate(row.GetValue("FechaUtc")) ?? DateTimeOffset.MinValue,
            row.GetValue("RepuestoCodigo")?.Trim() ?? string.Empty,
            row.GetValue("BodegaCodigo")?.Trim() ?? string.Empty,
            reserved,
            delivered,
            released,
            Math.Max(0, reserved - delivered - released),
            row.GetValue("WorkOrderId")?.Trim() ?? string.Empty,
            row.GetValue("Solicitante")?.Trim() ?? string.Empty,
            row.GetValue("Motivo")?.Trim() ?? string.Empty,
            row.GetValue("UsuarioId")?.Trim() ?? string.Empty);
    }

    private static StockTransferResponse ToTransfer(
        DataRow row,
        IReadOnlyCollection<StockMovementResponse> movements)
    {
        return new StockTransferResponse(
            row.GetValue("TransferenciaId")?.Trim() ?? string.Empty,
            ParseEnum(row.GetValue("Estado"), StockTransferStatus.EnTransito),
            ParseDate(row.GetValue("FechaSolicitudUtc")) ?? DateTimeOffset.MinValue,
            ParseDate(row.GetValue("FechaRecepcionUtc")),
            row.GetValue("RepuestoCodigo")?.Trim() ?? string.Empty,
            row.GetValue("BodegaOrigenCodigo")?.Trim() ?? string.Empty,
            row.GetValue("BodegaTransitoCodigo")?.Trim() ?? string.Empty,
            row.GetValue("BodegaDestinoCodigo")?.Trim() ?? string.Empty,
            ParseDecimal(row.GetValue("Cantidad")),
            row.GetValue("Motivo")?.Trim() ?? string.Empty,
            row.GetValue("UsuarioId")?.Trim() ?? string.Empty,
            EmptyToNull(row.GetValue("RecibidoPor")),
            EmptyToNull(row.GetValue("MotivoRecepcion")),
            movements.OrderByDescending(item => item.FechaUtc).ToArray());
    }

    private static DataRow SparePartRow(string code, CreateSparePartRequest request)
    {
        return SparePartRow(
            code,
            request.Descripcion,
            request.UnidadMedida,
            request.CodigoSap,
            request.CodigoProveedor,
            request.DescripcionTecnica,
            request.FamiliaEquipo,
            request.MarcaFabricante,
            request.ModeloReferencia,
            request.Critico,
            request.StockMinimo,
            request.StockMaximo,
            request.PuntoReposicion,
            request.LeadTimeEsperadoDias,
            request.CostoUnitarioPromedio,
            request.Estado,
            request.ProveedorPreferente,
            request.ReemplazoCodigo);
    }

    private static DataRow SparePartRow(string code, UpdateSparePartRequest request)
    {
        return SparePartRow(
            code,
            request.Descripcion,
            request.UnidadMedida,
            request.CodigoSap,
            request.CodigoProveedor,
            request.DescripcionTecnica,
            request.FamiliaEquipo,
            request.MarcaFabricante,
            request.ModeloReferencia,
            request.Critico,
            request.StockMinimo,
            request.StockMaximo,
            request.PuntoReposicion,
            request.LeadTimeEsperadoDias,
            request.CostoUnitarioPromedio,
            request.Estado,
            request.ProveedorPreferente,
            request.ReemplazoCodigo);
    }

    private static DataRow SparePartRow(
        string code,
        string description,
        string unitOfMeasure,
        string? sapCode,
        string? supplierCode,
        string? technicalDescription,
        string? equipmentFamily,
        string? manufacturer,
        string? modelReference,
        bool critical,
        decimal? minimum,
        decimal? maximum,
        decimal? reorderPoint,
        int? leadTimeDays,
        decimal? averageCost,
        SparePartStatus status,
        string? preferredSupplier,
        string? replacementCode)
    {
        var normalizedSap = EmptyToNull(sapCode)?.ToUpperInvariant();
        var now = DateTimeOffset.UtcNow.ToString("O");
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Codigo"] = NormalizeCode(code),
            ["CodigoSap"] = normalizedSap,
            ["CodigoProveedor"] = EmptyToNull(supplierCode),
            ["Descripcion"] = description.Trim(),
            ["DescripcionTecnica"] = EmptyToNull(technicalDescription) ?? description.Trim(),
            ["UnidadMedida"] = unitOfMeasure.Trim(),
            ["Familia"] = EmptyToNull(equipmentFamily),
            ["FamiliaEquipo"] = EmptyToNull(equipmentFamily),
            ["Marca"] = EmptyToNull(manufacturer),
            ["MarcaFabricante"] = EmptyToNull(manufacturer),
            ["Modelo"] = EmptyToNull(modelReference),
            ["ModeloReferencia"] = EmptyToNull(modelReference),
            ["Critico"] = critical.ToString(),
            ["StockMinimo"] = FormatDecimal(minimum ?? 0),
            ["StockMaximo"] = FormatDecimal(maximum ?? 0),
            ["PuntoReposicion"] = FormatDecimal(reorderPoint ?? 0),
            ["LeadTimeEsperadoDias"] = (leadTimeDays ?? 0).ToString(CultureInfo.InvariantCulture),
            ["CostoUnitarioPromedio"] = FormatDecimal(averageCost ?? 0),
            ["Estado"] = status.ToString(),
            ["EsNoCodificado"] = string.IsNullOrWhiteSpace(normalizedSap).ToString(),
            ["ProveedorPreferente"] = EmptyToNull(preferredSupplier),
            ["ReemplazoCodigo"] = EmptyToNull(replacementCode),
            ["FechaAltaUtc"] = now,
            ["ActualizadoEnUtc"] = now
        });
    }

    private static DataRow WarehouseRow(
        string code,
        string name,
        string faenaCodigo,
        WarehouseType type,
        string? location,
        IReadOnlyCollection<string> internalLocations,
        bool active,
        string? responsible,
        bool allowsNegative)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Codigo"] = NormalizeCode(code),
            ["Nombre"] = name.Trim(),
            ["FaenaCodigo"] = NormalizeCode(faenaCodigo),
            ["TipoBodega"] = type.ToString(),
            ["EsCentral"] = (type == WarehouseType.Central).ToString(),
            ["Ubicacion"] = EmptyToNull(location),
            ["UbicacionesInternas"] = string.Join(';', internalLocations.Select(item => item.Trim()).Where(item => item.Length > 0)),
            ["Activa"] = active.ToString(),
            ["Responsable"] = EmptyToNull(responsible),
            ["PermiteStockNegativo"] = allowsNegative.ToString()
        });
    }

    private static DataRow StockRow(
        string warehouseCode,
        string sparePartCode,
        decimal physical,
        decimal reserved,
        decimal minimum,
        decimal maximum,
        decimal reorderPoint,
        DateTimeOffset updatedAtUtc)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["BodegaCodigo"] = NormalizeCode(warehouseCode),
            ["RepuestoCodigo"] = NormalizeCode(sparePartCode),
            ["StockFisico"] = FormatDecimal(physical),
            ["StockReservado"] = FormatDecimal(reserved),
            ["StockDisponible"] = FormatDecimal(physical - reserved),
            ["StockMinimo"] = FormatDecimal(minimum),
            ["StockMaximo"] = FormatDecimal(maximum),
            ["PuntoReposicion"] = FormatDecimal(reorderPoint),
            ["ActualizadoEnUtc"] = updatedAtUtc.ToString("O")
        });
    }

    private static DataRow MovementRow(
        string id,
        DateTimeOffset date,
        StockMovementRequest request,
        string primaryWarehouse,
        StockMovementApplicationResult result,
        string userId)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["MovimientoId"] = id,
            ["FechaUtc"] = date.ToString("O"),
            ["Tipo"] = request.Type.ToString(),
            ["BodegaCodigo"] = NormalizeCode(primaryWarehouse),
            ["BodegaOrigenCodigo"] = EmptyToNull(request.SourceWarehouseCode),
            ["BodegaDestinoCodigo"] = EmptyToNull(request.TargetWarehouseCode),
            ["RepuestoCodigo"] = NormalizeCode(request.RepuestoCodigo),
            ["Cantidad"] = FormatDecimal(request.Quantity),
            ["StockFisicoAnterior"] = FormatDecimal(result.PreviousPhysical),
            ["StockFisicoNuevo"] = FormatDecimal(result.NextPhysical),
            ["StockReservadoAnterior"] = FormatDecimal(result.PreviousReserved),
            ["StockReservadoNuevo"] = FormatDecimal(result.NextReserved),
            ["Motivo"] = request.Reason.Trim(),
            ["UsuarioId"] = userId,
            ["ReferenciaTipo"] = EmptyToNull(request.ReferenceType),
            ["ReferenciaId"] = EmptyToNull(request.ReferenceId),
            ["PermiteNegativoExcepcional"] = request.AllowNegativeException.ToString()
        });
    }

    private static DataRow ReservationRow(
        string id,
        StockReservationStatus status,
        DateTimeOffset date,
        string sparePartCode,
        string warehouseCode,
        decimal reserved,
        decimal delivered,
        decimal released,
        string workOrderId,
        string requestedBy,
        string reason,
        string userId)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ReservaId"] = NormalizeCode(id),
            ["Estado"] = status.ToString(),
            ["FechaUtc"] = date.ToString("O"),
            ["RepuestoCodigo"] = NormalizeCode(sparePartCode),
            ["BodegaCodigo"] = NormalizeCode(warehouseCode),
            ["CantidadReservada"] = FormatDecimal(reserved),
            ["CantidadEntregada"] = FormatDecimal(delivered),
            ["CantidadLiberada"] = FormatDecimal(released),
            ["WorkOrderId"] = workOrderId.Trim(),
            ["Solicitante"] = requestedBy.Trim(),
            ["Motivo"] = reason.Trim(),
            ["UsuarioId"] = userId
        });
    }

    private static DataRow TransferRow(
        string id,
        StockTransferStatus status,
        DateTimeOffset requestedAtUtc,
        DateTimeOffset? receivedAtUtc,
        string sparePartCode,
        string sourceWarehouseCode,
        string transitWarehouseCode,
        string targetWarehouseCode,
        decimal quantity,
        string reason,
        string userId,
        string? receivedBy,
        string? receiveReason)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["TransferenciaId"] = NormalizeCode(id),
            ["Estado"] = status.ToString(),
            ["FechaSolicitudUtc"] = requestedAtUtc.ToString("O"),
            ["FechaRecepcionUtc"] = receivedAtUtc?.ToString("O"),
            ["RepuestoCodigo"] = NormalizeCode(sparePartCode),
            ["BodegaOrigenCodigo"] = NormalizeCode(sourceWarehouseCode),
            ["BodegaTransitoCodigo"] = NormalizeCode(transitWarehouseCode),
            ["BodegaDestinoCodigo"] = NormalizeCode(targetWarehouseCode),
            ["Cantidad"] = FormatDecimal(quantity),
            ["Motivo"] = reason.Trim(),
            ["UsuarioId"] = userId,
            ["RecibidoPor"] = EmptyToNull(receivedBy),
            ["MotivoRecepcion"] = EmptyToNull(receiveReason)
        });
    }

    private static void ValidateSparePartRequest(
        string description,
        string unitOfMeasure,
        decimal? minimum,
        decimal? maximum,
        decimal? reorderPoint,
        int? leadTimeDays)
    {
        ValidateRequired(description, nameof(description));
        ValidateRequired(unitOfMeasure, nameof(unitOfMeasure));
        if (minimum < 0 || maximum < 0 || reorderPoint < 0 || leadTimeDays < 0)
        {
            throw new DomainException("Los parametros de stock y lead time no pueden ser negativos.");
        }

        if (maximum > 0 && minimum > maximum)
        {
            throw new DomainException("El stock minimo no puede ser mayor al stock maximo.");
        }
    }

    private static string NextSparePartCode(IReadOnlyCollection<DataRow> rows)
    {
        var next = rows
            .Select(row => row.GetValue("Codigo"))
            .Where(value => !string.IsNullOrWhiteSpace(value) && value.StartsWith("REP-", StringComparison.OrdinalIgnoreCase))
            .Select(value => int.TryParse(value![4..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"REP-{next:000000}";
    }

    private static void EnsureUniqueSap(IReadOnlyCollection<DataRow> rows, string? sapCode, string? currentCode)
    {
        var normalizedSap = EmptyToNull(sapCode)?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSap))
        {
            return;
        }

        var duplicate = rows.FirstOrDefault(row =>
            Same(row.GetValue("CodigoSap"), normalizedSap) &&
            !Same(row.GetValue("Codigo"), currentCode));
        if (duplicate is not null)
        {
            throw new DomainException($"Ya existe un repuesto con codigo SAP '{normalizedSap}'.");
        }
    }

    private static string ResolvePrimaryWarehouseCode(StockMovementRequest request)
    {
        return request.Type switch
        {
            StockMovementType.TransferOut => EmptyToNull(request.SourceWarehouseCode) ?? EmptyToNull(request.BodegaCodigo) ?? string.Empty,
            StockMovementType.TransferIn => EmptyToNull(request.TargetWarehouseCode) ?? EmptyToNull(request.BodegaCodigo) ?? string.Empty,
            StockMovementType.TransferReception => EmptyToNull(request.TargetWarehouseCode) ?? EmptyToNull(request.BodegaCodigo) ?? string.Empty,
            StockMovementType.InTransit => EmptyToNull(request.TargetWarehouseCode) ?? EmptyToNull(request.BodegaCodigo) ?? string.Empty,
            StockMovementType.Reception => EmptyToNull(request.TargetWarehouseCode) ?? EmptyToNull(request.BodegaCodigo) ?? string.Empty,
            _ => EmptyToNull(request.BodegaCodigo) ?? EmptyToNull(request.SourceWarehouseCode) ?? EmptyToNull(request.TargetWarehouseCode) ?? string.Empty
        };
    }

    private static bool IsTransferPairMovement(StockMovementType type)
    {
        return type is StockMovementType.TransferOut or StockMovementType.TransferIn or StockMovementType.TransferReception;
    }

    private static StockMovementType ResolveSecondaryTransferType(
        StockMovementRequest request,
        string primaryWarehouse,
        IReadOnlyCollection<DataRow> warehouseRows)
    {
        var primaryIsSource = Same(primaryWarehouse, request.SourceWarehouseCode);
        if (!primaryIsSource)
        {
            return StockMovementType.TransferOut;
        }

        var target = warehouseRows.FirstOrDefault(row => Same(row.GetValue("Codigo"), request.TargetWarehouseCode));
        var targetType = target is null
            ? WarehouseType.Faena
            : ParseEnum(target.GetValue("TipoBodega"), WarehouseType.Faena);
        return targetType == WarehouseType.Transito
            ? StockMovementType.InTransit
            : StockMovementType.TransferIn;
    }

    private static string BuildConsumptionReferenceType(DeliverMaterialRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.WorkOrderId))
        {
            return "OT";
        }

        if (!string.IsNullOrWhiteSpace(request.AssetCode))
        {
            return "Activo";
        }

        if (!string.IsNullOrWhiteSpace(request.FaenaCodigo))
        {
            return "Faena";
        }

        return "CentroCosto";
    }

    private static string? ResolveFaena(IReadOnlyCollection<DataRow> warehouseRows, string warehouseCode)
    {
        return warehouseRows.FirstOrDefault(row => Same(row.GetValue("Codigo"), warehouseCode))?.GetValue("FaenaCodigo");
    }

    private static bool MatchesSearch(SparePartSummary item, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var value = search.Trim();
        return Contains(item.Codigo, value) ||
               Contains(item.CodigoSap, value) ||
               Contains(item.CodigoProveedor, value) ||
               Contains(item.Descripcion, value) ||
               Contains(item.DescripcionTecnica, value) ||
               Contains(item.ProveedorPreferente, value) ||
               Contains(item.FamiliaEquipo, value);
    }

    private static void EnsurePositive(decimal value)
    {
        if (value <= 0)
        {
            throw new DomainException("La cantidad debe ser mayor a cero para este tipo de movimiento.");
        }
    }

    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} es requerido.");
        }
    }

    private async Task RecordAuditAsync(
        UserAccessContext user,
        string action,
        string module,
        string entityName,
        string entityId,
        string? previous,
        string? next,
        string? faenaCodigo,
        string? reason,
        CancellationToken cancellationToken,
        AuditSeverity severity = AuditSeverity.Medium)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            module,
            entityName,
            entityId,
            PreviousValue: previous,
            NewValue: next,
            FaenaCodigo: faenaCodigo,
            Severity: severity,
            Reason: reason), cancellationToken);
    }

    private static string Serialize(DataRow row)
    {
        return JsonSerializer.Serialize(row.Values);
    }

    private static IReadOnlyCollection<string> SplitList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static decimal ParseDecimal(string? value, decimal fallback = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariant) ||
               decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out invariant)
            ? invariant
            : fallback;
    }

    private static int ParseInt(string? value, int fallback = 0)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool ParseBool(string? value, bool fallback = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return bool.TryParse(value, out var parsed)
            ? parsed
            : value.Equals("SI", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool Same(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string? value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record StockMovementApplicationResult(
        decimal PreviousPhysical,
        decimal NextPhysical,
        decimal PreviousReserved,
        decimal NextReserved);
}
