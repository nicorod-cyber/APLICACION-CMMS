using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.Procurement;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Infrastructure.Procurement;

public sealed class ProcurementService : IProcurementService
{
    private const string SuppliersSchema = "proveedores";
    private const string RequestsSchema = "abastecimiento_solicitudes";
    private const string PurchaseOrdersSchema = "ordenes_compra";
    private const string ReceiptsSchema = "recepciones_abastecimiento";
    private const string MaterialRequestsSchema = "solicitudes_repuestos";

    private readonly IDataProvider _dataProvider;
    private readonly IInventoryService _inventoryService;
    private readonly IAuditService _auditService;

    public ProcurementService(
        IDataProvider dataProvider,
        IInventoryService inventoryService,
        IAuditService auditService)
    {
        _dataProvider = dataProvider;
        _inventoryService = inventoryService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyCollection<SupplierResponse>> ListSuppliersAsync(
        SupplierQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        return (await _dataProvider.ReadRowsAsync(SuppliersSchema, cancellationToken))
            .Select(ToSupplier)
            .Where(item => query.IncludeInactive || item.Activo)
            .Where(item => string.IsNullOrWhiteSpace(query.Search) || Contains(item.Rut, query.Search) || Contains(item.Nombre, query.Search) || Contains(item.Contacto, query.Search))
            .OrderBy(item => item.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<SupplierResponse?> GetSupplierAsync(
        string rut,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        return (await _dataProvider.ReadRowsAsync(SuppliersSchema, cancellationToken))
            .Select(ToSupplier)
            .FirstOrDefault(item => Same(item.Rut, rut));
    }

    public async Task<SupplierResponse> CreateSupplierAsync(
        UpsertSupplierRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateSupplier(request);

        var rows = (await _dataProvider.ReadRowsAsync(SuppliersSchema, cancellationToken)).ToList();
        if (rows.Any(row => Same(row.GetValue("Rut"), request.Rut)))
        {
            throw new DomainException($"Ya existe el proveedor '{request.Rut}'.");
        }

        var rowToCreate = SupplierRow(request);
        rows.Add(rowToCreate);
        await _dataProvider.SaveRowsAsync(SuppliersSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "procurement.supplier_created", "Supplier", request.Rut, null, rowToCreate, null, request.Observaciones, cancellationToken);
        return ToSupplier(rowToCreate);
    }

    public async Task<SupplierResponse?> UpdateSupplierAsync(
        string rut,
        UpsertSupplierRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateSupplier(request);

        var rows = (await _dataProvider.ReadRowsAsync(SuppliersSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("Rut"), rut));
        if (index < 0)
        {
            return null;
        }

        if (!Same(rut, request.Rut) && rows.Any(row => Same(row.GetValue("Rut"), request.Rut)))
        {
            throw new DomainException($"Ya existe el proveedor '{request.Rut}'.");
        }

        var previous = rows[index];
        var updated = SupplierRow(request);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(SuppliersSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "procurement.supplier_updated", "Supplier", request.Rut, previous, updated, null, request.Observaciones, cancellationToken);
        return ToSupplier(updated);
    }

    public async Task<IReadOnlyCollection<ProcurementRequestResponse>> ListRequestsAsync(
        ProcurementRequestQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var suppliers = await SupplierDictionaryAsync(cancellationToken);

        return (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken))
            .Select(row => ToRequest(row, suppliers))
            .Where(item => query.IncludeClosed || item.Estado is not (ProcurementRequestStatus.Cerrada or ProcurementRequestStatus.Cancelada))
            .Where(item => !query.Status.HasValue || item.Estado == query.Status)
            .Where(item => string.IsNullOrWhiteSpace(query.SupplierRut) || Same(item.ProveedorRut, query.SupplierRut))
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.RepuestoCodigo) || Same(item.RepuestoCodigo, query.RepuestoCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.SolicitudInternaCmms) || Same(item.SolicitudInternaCmms, query.SolicitudInternaCmms))
            .Where(item => !query.OverdueOnly || item.EstaVencida)
            .Where(item => CanAccessFaena(user, item.FaenaCodigo))
            .OrderByDescending(item => item.FechaEnvioAbastecimiento)
            .ToArray();
    }

    public async Task<ProcurementRequestResponse?> GetRequestAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var suppliers = await SupplierDictionaryAsync(cancellationToken);
        var row = (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken))
            .FirstOrDefault(item => Same(item.GetValue("SolicitudId"), id));
        if (row is null)
        {
            return null;
        }

        var response = ToRequest(row, suppliers);
        EnsureFaenaAccess(user, response.FaenaCodigo);
        return response;
    }

    public async Task<ProcurementRequestResponse> CreateRequestAsync(
        CreateProcurementRequestRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);

        var materialRequest = string.IsNullOrWhiteSpace(request.SolicitudInternaCmms)
            ? null
            : await FindMaterialRequestAsync(request.SolicitudInternaCmms, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var fechaEnvioAbastecimiento = request.FechaEnvioAbastecimiento ?? now;
        var description = FirstNonEmpty(request.Descripcion, materialRequest?.GetValue("DescripcionTecnica"));
        var quantity = request.Cantidad > 0 ? request.Cantidad : ParseDecimal(materialRequest?.GetValue("Cantidad"));
        var unit = FirstNonEmpty(request.Unidad, materialRequest?.GetValue("Unidad"));
        var faena = FirstNonEmpty(request.FaenaCodigo, materialRequest?.GetValue("FaenaCodigo"));
        EnsureFaenaAccess(user, faena);

        ValidateRequired(description, nameof(request.Descripcion));
        ValidateRequired(unit, nameof(request.Unidad));
        ValidateRequired(request.Motivo, nameof(request.Motivo));
        EnsurePositive(quantity);

        var rows = (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken)).ToList();
        var id = NextRequestId(rows);
        var fechaSolicitudTecnica = request.FechaSolicitudTecnica
            ?? ParseDate(materialRequest?.GetValue("SolicitadoEnUtc"))
            ?? now;
        var fechaAprobacion = request.FechaAprobacionMantenimiento
            ?? ParseDate(materialRequest?.GetValue("AprobadoMantenimientoEnUtc"));

        var row = RequestRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SolicitudId"] = id,
            ["Estado"] = ProcurementRequestStatus.EnviadaAbastecimiento.ToString(),
            ["SolicitudInternaCmms"] = NormalizeCode(request.SolicitudInternaCmms),
            ["SolicitudExternaNumero"] = NormalizeText(request.SolicitudExternaNumero),
            ["RepuestoCodigo"] = NormalizeCode(FirstNonEmpty(request.RepuestoCodigo, materialRequest?.GetValue("RepuestoMaestroCodigo"), materialRequest?.GetValue("RepuestoCodigo"))),
            ["Descripcion"] = description,
            ["Cantidad"] = FormatNumber(quantity),
            ["Unidad"] = unit,
            ["CantidadRecibida"] = "0",
            ["CantidadEntregada"] = "0",
            ["FaenaCodigo"] = NormalizeCode(faena),
            ["BodegaCodigo"] = NormalizeCode(FirstNonEmpty(request.BodegaCodigo, materialRequest?.GetValue("BodegaCodigo"))),
            ["OtNumero"] = NormalizeText(FirstNonEmpty(request.OtNumero, materialRequest?.GetValue("OT"))),
            ["ActivoCodigo"] = NormalizeText(FirstNonEmpty(request.ActivoCodigo, materialRequest?.GetValue("ActivoCodigo"))),
            ["Motivo"] = NormalizeText(request.Motivo),
            ["FechaSolicitudTecnica"] = FormatDate(fechaSolicitudTecnica),
            ["FechaAprobacionMantenimiento"] = fechaAprobacion is null ? null : FormatDate(fechaAprobacion.Value),
            ["FechaEnvioAbastecimiento"] = FormatDate(fechaEnvioAbastecimiento),
            ["CostoEstimado"] = FormatOptionalNumber(request.CostoEstimado),
            ["Moneda"] = NormalizeText(request.Moneda) ?? "CLP",
            ["DocumentoRespaldoUrl"] = NormalizeText(request.DocumentoRespaldoUrl),
            ["CreadoPor"] = user.UserId,
            ["CreadoEnUtc"] = FormatDate(now),
            ["Observaciones"] = "Solicitud enviada a abastecimiento"
        });

        rows.Add(row);
        await _dataProvider.SaveRowsAsync(RequestsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "procurement.request_created", "ProcurementRequest", id, null, row, faena, request.Motivo, cancellationToken);
        return ToRequest(row, await SupplierDictionaryAsync(cancellationToken));
    }

    public async Task<ProcurementRequestResponse?> LinkPurchaseOrderAsync(
        string id,
        LinkPurchaseOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.OcNumero, nameof(request.OcNumero));
        ValidateRequired(request.ProveedorRut, nameof(request.ProveedorRut));
        ValidateRequired(request.Reason, nameof(request.Reason));
        if (await GetSupplierAsync(request.ProveedorRut, user, cancellationToken) is null)
        {
            throw new DomainException($"El proveedor '{request.ProveedorRut}' no existe.");
        }

        var result = await UpdateRequestAsync(id, user, cancellationToken, async (current, values) =>
        {
            values["Estado"] = ProcurementRequestStatus.OCAsociada.ToString();
            values["SolicitudExternaNumero"] = NormalizeText(request.SolicitudExternaNumero) ?? current.SolicitudExternaNumero;
            values["OcNumero"] = NormalizeCode(request.OcNumero);
            values["ProveedorRut"] = NormalizeCode(request.ProveedorRut);
            values["FechaOC"] = FormatDate(request.FechaOC ?? DateTimeOffset.UtcNow);
            values["FechaComprometida"] = FormatDate(request.FechaComprometida);
            values["CostoOC"] = FormatOptionalNumber(request.CostoOC);
            values["Moneda"] = NormalizeText(request.Moneda) ?? current.Moneda;
            values["DocumentoOcUrl"] = NormalizeText(request.DocumentoOcUrl);
            values["ActualizadoPor"] = user.UserId;
            values["ActualizadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"OC {request.OcNumero}: {request.Reason}");

            var purchaseOrderRows = (await _dataProvider.ReadRowsAsync(PurchaseOrdersSchema, cancellationToken)).ToList();
            var purchaseOrderRow = PurchaseOrderRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["OrdenCompraId"] = $"OCREF-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                ["SolicitudId"] = current.SolicitudId,
                ["SolicitudExternaNumero"] = NormalizeText(request.SolicitudExternaNumero) ?? current.SolicitudExternaNumero,
                ["OcNumero"] = NormalizeCode(request.OcNumero),
                ["ProveedorRut"] = NormalizeCode(request.ProveedorRut),
                ["FechaOC"] = FormatDate(request.FechaOC ?? DateTimeOffset.UtcNow),
                ["FechaComprometida"] = FormatDate(request.FechaComprometida),
                ["CostoOC"] = FormatOptionalNumber(request.CostoOC),
                ["Moneda"] = NormalizeText(request.Moneda) ?? current.Moneda,
                ["DocumentoOcUrl"] = NormalizeText(request.DocumentoOcUrl),
                ["UsuarioId"] = user.UserId,
                ["Motivo"] = request.Reason,
                ["CreadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow)
            });
            purchaseOrderRows.Add(purchaseOrderRow);
            await _dataProvider.SaveRowsAsync(PurchaseOrdersSchema, purchaseOrderRows, cancellationToken);
            return ("procurement.purchase_order_linked", request.Reason);
        });

        return result;
    }

    public async Task<ProcurementRequestResponse?> RegisterReceptionAsync(
        string id,
        RegisterProcurementReceptionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.BodegaCodigo, nameof(request.BodegaCodigo));
        ValidateRequired(request.Reason, nameof(request.Reason));
        EnsurePositive(request.CantidadRecibida);

        return await UpdateRequestAsync(id, user, cancellationToken, async (current, values) =>
        {
            if (string.IsNullOrWhiteSpace(current.OcNumero))
            {
                throw new DomainException("Debe asociar una OC antes de registrar recepcion.");
            }

            var fechaRecepcion = request.FechaRecepcion ?? DateTimeOffset.UtcNow;
            string? movementReceptionId = null;
            string? movementDeliveryId = null;
            if (!string.IsNullOrWhiteSpace(current.RepuestoCodigo))
            {
                var reception = await _inventoryService.RegisterMovementAsync(
                    new StockMovementRequest(
                        StockMovementType.Reception,
                        current.RepuestoCodigo,
                        request.CantidadRecibida,
                        $"Abastecimiento {current.SolicitudId}: {request.Reason}",
                        BodegaCodigo: request.BodegaCodigo,
                        ReferenceType: "Abastecimiento",
                        ReferenceId: current.SolicitudId),
                    user,
                    cancellationToken);
                movementReceptionId = reception.MovimientoId;

                if (request.DespachoDirectoOt)
                {
                    var delivery = await _inventoryService.DeliverMaterialAsync(
                        new DeliverMaterialRequest(
                            current.RepuestoCodigo,
                            request.BodegaCodigo,
                            request.CantidadRecibida,
                            $"Despacho directo {current.SolicitudId}: {request.Reason}",
                            WorkOrderId: FirstNonEmpty(request.OtNumero, current.OtNumero),
                            AssetCode: FirstNonEmpty(request.ActivoCodigo, current.ActivoCodigo),
                            FaenaCodigo: FirstNonEmpty(request.FaenaCodigo, current.FaenaCodigo)),
                        user,
                        cancellationToken);
                    movementDeliveryId = delivery.MovimientoId;
                }
            }
            else if (request.DespachoDirectoOt)
            {
                throw new DomainException("El despacho directo requiere repuesto codificado.");
            }

            var received = current.CantidadRecibida + request.CantidadRecibida;
            var delivered = current.CantidadEntregada + (request.DespachoDirectoOt ? request.CantidadRecibida : 0);
            values["CantidadRecibida"] = FormatNumber(received);
            values["CantidadEntregada"] = FormatNumber(delivered);
            values["BodegaCodigo"] = NormalizeCode(request.BodegaCodigo);
            values["FechaRecepcion"] = values.GetValueOrDefault("FechaRecepcion") ?? FormatDate(fechaRecepcion);
            values["FechaEntrega"] = request.DespachoDirectoOt
                ? FormatDate(request.FechaEntrega ?? fechaRecepcion)
                : values.GetValueOrDefault("FechaEntrega");
            values["CostoReal"] = FormatOptionalNumber(request.CostoReal) ?? values.GetValueOrDefault("CostoReal");
            values["DocumentoRecepcionUrl"] = NormalizeText(request.DocumentoRecepcionUrl) ?? values.GetValueOrDefault("DocumentoRecepcionUrl");
            values["DocumentoEntregaUrl"] = NormalizeText(request.DocumentoEntregaUrl) ?? values.GetValueOrDefault("DocumentoEntregaUrl");
            values["Estado"] = ResolveStatus(current.Cantidad, received, delivered, request.DespachoDirectoOt).ToString();
            values["ActualizadoPor"] = user.UserId;
            values["ActualizadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Recepcion {request.CantidadRecibida} {current.Unidad}: {request.Reason}");

            var receiptRows = (await _dataProvider.ReadRowsAsync(ReceiptsSchema, cancellationToken)).ToList();
            receiptRows.Add(ReceiptRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["RecepcionId"] = $"REC-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                ["SolicitudId"] = current.SolicitudId,
                ["OcNumero"] = current.OcNumero,
                ["FechaRecepcion"] = FormatDate(fechaRecepcion),
                ["CantidadRecibida"] = FormatNumber(request.CantidadRecibida),
                ["CantidadDespachada"] = FormatNumber(request.DespachoDirectoOt ? request.CantidadRecibida : 0),
                ["BodegaCodigo"] = NormalizeCode(request.BodegaCodigo),
                ["DespachoDirectoOt"] = request.DespachoDirectoOt.ToString(CultureInfo.InvariantCulture),
                ["OtNumero"] = FirstNonEmpty(request.OtNumero, current.OtNumero),
                ["ActivoCodigo"] = FirstNonEmpty(request.ActivoCodigo, current.ActivoCodigo),
                ["FaenaCodigo"] = FirstNonEmpty(request.FaenaCodigo, current.FaenaCodigo),
                ["MovimientoRecepcionId"] = movementReceptionId,
                ["MovimientoEntregaId"] = movementDeliveryId,
                ["CostoReal"] = FormatOptionalNumber(request.CostoReal),
                ["DocumentoRecepcionUrl"] = NormalizeText(request.DocumentoRecepcionUrl),
                ["DocumentoEntregaUrl"] = NormalizeText(request.DocumentoEntregaUrl),
                ["UsuarioId"] = user.UserId,
                ["Motivo"] = request.Reason
            }));
            await _dataProvider.SaveRowsAsync(ReceiptsSchema, receiptRows, cancellationToken);
            return ("procurement.reception_registered", request.Reason);
        });
    }

    public async Task<ProcurementRequestResponse?> RegisterDeliveryAsync(
        string id,
        DeliverProcurementRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(user);
        ValidateRequired(request.BodegaCodigo, nameof(request.BodegaCodigo));
        ValidateRequired(request.Reason, nameof(request.Reason));
        EnsurePositive(request.CantidadEntregada);

        return await UpdateRequestAsync(id, user, cancellationToken, async (current, values) =>
        {
            if (string.IsNullOrWhiteSpace(current.RepuestoCodigo))
            {
                throw new DomainException("La entrega requiere repuesto codificado.");
            }

            var availableToDeliver = current.CantidadRecibida - current.CantidadEntregada;
            if (request.CantidadEntregada > availableToDeliver)
            {
                throw new DomainException("La cantidad a entregar excede lo recibido pendiente de despacho.");
            }

            var fechaEntrega = request.FechaEntrega ?? DateTimeOffset.UtcNow;
            var movement = await _inventoryService.DeliverMaterialAsync(
                new DeliverMaterialRequest(
                    current.RepuestoCodigo,
                    request.BodegaCodigo,
                    request.CantidadEntregada,
                    $"Entrega abastecimiento {current.SolicitudId}: {request.Reason}",
                    WorkOrderId: FirstNonEmpty(request.OtNumero, current.OtNumero),
                    AssetCode: FirstNonEmpty(request.ActivoCodigo, current.ActivoCodigo),
                    FaenaCodigo: FirstNonEmpty(request.FaenaCodigo, current.FaenaCodigo)),
                user,
                cancellationToken);

            var delivered = current.CantidadEntregada + request.CantidadEntregada;
            values["CantidadEntregada"] = FormatNumber(delivered);
            values["FechaEntrega"] = FormatDate(fechaEntrega);
            values["DocumentoEntregaUrl"] = NormalizeText(request.DocumentoEntregaUrl) ?? values.GetValueOrDefault("DocumentoEntregaUrl");
            values["Estado"] = ResolveStatus(current.Cantidad, current.CantidadRecibida, delivered, true).ToString();
            values["ActualizadoPor"] = user.UserId;
            values["ActualizadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Entrega {request.CantidadEntregada} {current.Unidad}: {request.Reason}");

            var receiptRows = (await _dataProvider.ReadRowsAsync(ReceiptsSchema, cancellationToken)).ToList();
            receiptRows.Add(ReceiptRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["RecepcionId"] = $"DESP-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                ["SolicitudId"] = current.SolicitudId,
                ["OcNumero"] = current.OcNumero,
                ["FechaRecepcion"] = FormatDate(current.FechaRecepcion ?? fechaEntrega),
                ["CantidadRecibida"] = "0",
                ["CantidadDespachada"] = FormatNumber(request.CantidadEntregada),
                ["BodegaCodigo"] = NormalizeCode(request.BodegaCodigo),
                ["DespachoDirectoOt"] = "False",
                ["OtNumero"] = FirstNonEmpty(request.OtNumero, current.OtNumero),
                ["ActivoCodigo"] = FirstNonEmpty(request.ActivoCodigo, current.ActivoCodigo),
                ["FaenaCodigo"] = FirstNonEmpty(request.FaenaCodigo, current.FaenaCodigo),
                ["MovimientoEntregaId"] = movement.MovimientoId,
                ["DocumentoEntregaUrl"] = NormalizeText(request.DocumentoEntregaUrl),
                ["UsuarioId"] = user.UserId,
                ["Motivo"] = request.Reason
            }));
            await _dataProvider.SaveRowsAsync(ReceiptsSchema, receiptRows, cancellationToken);
            return ("procurement.delivery_registered", request.Reason);
        });
    }

    private async Task<ProcurementRequestResponse?> UpdateRequestAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken,
        Func<ProcurementRequestResponse, Dictionary<string, string?>, Task<(string Action, string? Reason)>> mutate)
    {
        var suppliers = await SupplierDictionaryAsync(cancellationToken);
        var rows = (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("SolicitudId"), id));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var current = ToRequest(previous, suppliers);
        EnsureFaenaAccess(user, current.FaenaCodigo);
        var values = CopyRequest(previous);
        var (action, reason) = await mutate(current, values);
        var updated = RequestRow(values);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(RequestsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, action, "ProcurementRequest", current.SolicitudId, previous, updated, current.FaenaCodigo, reason, cancellationToken, AuditSeverity.High);
        return ToRequest(updated, await SupplierDictionaryAsync(cancellationToken));
    }

    private async Task<Dictionary<string, SupplierResponse>> SupplierDictionaryAsync(CancellationToken cancellationToken)
    {
        return (await _dataProvider.ReadRowsAsync(SuppliersSchema, cancellationToken))
            .Select(ToSupplier)
            .ToDictionary(item => item.Rut, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<DataRow?> FindMaterialRequestAsync(string requestNumber, CancellationToken cancellationToken)
    {
        return (await _dataProvider.ReadRowsAsync(MaterialRequestsSchema, cancellationToken))
            .FirstOrDefault(row => Same(row.GetValue("NumeroSolicitud"), requestNumber));
    }

    private static SupplierResponse ToSupplier(DataRow row)
    {
        return new SupplierResponse(
            row.GetValue("Rut") ?? string.Empty,
            row.GetValue("Nombre") ?? string.Empty,
            EmptyToNull(row.GetValue("Contacto")),
            EmptyToNull(row.GetValue("Email")),
            EmptyToNull(row.GetValue("Telefono")),
            EmptyToNull(row.GetValue("Direccion")),
            (int)ParseDecimal(row.GetValue("LeadTimeEsperadoDias")),
            ParseBool(row.GetValue("Activo"), true),
            EmptyToNull(row.GetValue("Observaciones")));
    }

    private static ProcurementRequestResponse ToRequest(DataRow row, IReadOnlyDictionary<string, SupplierResponse> suppliers)
    {
        var status = ParseEnum(row.GetValue("Estado"), ProcurementRequestStatus.EnviadaAbastecimiento);
        var supplierRut = EmptyToNull(row.GetValue("ProveedorRut"));
        suppliers.TryGetValue(supplierRut ?? string.Empty, out var supplier);
        var fechaSolicitudTecnica = ParseDate(row.GetValue("FechaSolicitudTecnica")) ?? DateTimeOffset.MinValue;
        var fechaAprobacion = ParseDate(row.GetValue("FechaAprobacionMantenimiento"));
        var fechaEnvio = ParseDate(row.GetValue("FechaEnvioAbastecimiento")) ?? fechaSolicitudTecnica;
        var fechaOC = ParseDate(row.GetValue("FechaOC"));
        var fechaComprometida = ParseDate(row.GetValue("FechaComprometida"));
        var fechaRecepcion = ParseDate(row.GetValue("FechaRecepcion"));
        var fechaEntrega = ParseDate(row.GetValue("FechaEntrega"));
        var leadTime = BuildLeadTime(fechaSolicitudTecnica, fechaAprobacion, fechaEnvio, fechaOC, fechaRecepcion, fechaEntrega);
        var isClosed = status is ProcurementRequestStatus.Entregada or ProcurementRequestStatus.Cerrada or ProcurementRequestStatus.Cancelada;
        var isOverdue = fechaComprometida.HasValue && fechaComprometida.Value.UtcDateTime.Date < DateTimeOffset.UtcNow.UtcDateTime.Date && !isClosed;

        return new ProcurementRequestResponse(
            row.GetValue("SolicitudId") ?? string.Empty,
            status,
            EmptyToNull(row.GetValue("SolicitudInternaCmms")),
            EmptyToNull(row.GetValue("SolicitudExternaNumero")),
            EmptyToNull(row.GetValue("OcNumero")),
            supplierRut,
            supplier?.Nombre,
            EmptyToNull(row.GetValue("RepuestoCodigo")),
            row.GetValue("Descripcion") ?? string.Empty,
            ParseDecimal(row.GetValue("Cantidad")),
            row.GetValue("Unidad") ?? string.Empty,
            ParseDecimal(row.GetValue("CantidadRecibida")),
            ParseDecimal(row.GetValue("CantidadEntregada")),
            EmptyToNull(row.GetValue("FaenaCodigo")),
            EmptyToNull(row.GetValue("BodegaCodigo")),
            EmptyToNull(row.GetValue("OtNumero")),
            EmptyToNull(row.GetValue("ActivoCodigo")),
            row.GetValue("Motivo") ?? string.Empty,
            fechaSolicitudTecnica,
            fechaAprobacion,
            fechaEnvio,
            fechaOC,
            fechaComprometida,
            fechaRecepcion,
            fechaEntrega,
            ParseOptionalDecimal(row.GetValue("CostoEstimado")),
            ParseOptionalDecimal(row.GetValue("CostoOC")),
            ParseOptionalDecimal(row.GetValue("CostoReal")),
            row.GetValue("Moneda") ?? "CLP",
            EmptyToNull(row.GetValue("DocumentoRespaldoUrl")),
            EmptyToNull(row.GetValue("DocumentoOcUrl")),
            EmptyToNull(row.GetValue("DocumentoRecepcionUrl")),
            EmptyToNull(row.GetValue("DocumentoEntregaUrl")),
            leadTime,
            isOverdue,
            row.GetValue("CreadoPor") ?? string.Empty,
            ParseDate(row.GetValue("CreadoEnUtc")) ?? fechaEnvio,
            EmptyToNull(row.GetValue("ActualizadoPor")),
            ParseDate(row.GetValue("ActualizadoEnUtc")),
            EmptyToNull(row.GetValue("Observaciones")));
    }

    private static DataRow SupplierRow(UpsertSupplierRequest request)
    {
        return new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Rut"] = NormalizeCode(request.Rut),
            ["Nombre"] = NormalizeText(request.Nombre),
            ["Contacto"] = NormalizeText(request.Contacto),
            ["Email"] = NormalizeText(request.Email),
            ["Telefono"] = NormalizeText(request.Telefono),
            ["Direccion"] = NormalizeText(request.Direccion),
            ["LeadTimeEsperadoDias"] = FormatNumber(request.LeadTimeEsperadoDias ?? 0),
            ["Activo"] = request.Activo.ToString(CultureInfo.InvariantCulture),
            ["Observaciones"] = NormalizeText(request.Observaciones)
        });
    }

    private static DataRow RequestRow(IReadOnlyDictionary<string, string?> values) => Row(RequestColumns, values);

    private static DataRow PurchaseOrderRow(IReadOnlyDictionary<string, string?> values) => Row(PurchaseOrderColumns, values);

    private static DataRow ReceiptRow(IReadOnlyDictionary<string, string?> values) => Row(ReceiptColumns, values);

    private static DataRow Row(IEnumerable<string> columns, IReadOnlyDictionary<string, string?> values)
    {
        return new DataRow(columns.ToDictionary(column => column, column => values.TryGetValue(column, out var value) ? value : null, StringComparer.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string?> CopyRequest(DataRow row)
    {
        return RequestColumns.ToDictionary(column => column, column => row.GetValue(column), StringComparer.OrdinalIgnoreCase);
    }

    private static ProcurementRequestStatus ResolveStatus(decimal requested, decimal received, decimal delivered, bool deliveryTouched)
    {
        if (deliveryTouched && delivered >= requested)
        {
            return ProcurementRequestStatus.Entregada;
        }

        if (received >= requested)
        {
            return ProcurementRequestStatus.Recepcionada;
        }

        return ProcurementRequestStatus.RecepcionParcial;
    }

    private static LeadTimeBreakdown BuildLeadTime(
        DateTimeOffset fechaSolicitudTecnica,
        DateTimeOffset? fechaAprobacion,
        DateTimeOffset fechaEnvio,
        DateTimeOffset? fechaOC,
        DateTimeOffset? fechaRecepcion,
        DateTimeOffset? fechaEntrega)
    {
        var totalEnd = fechaEntrega ?? fechaRecepcion ?? fechaOC ?? fechaEnvio;
        return new LeadTimeBreakdown(
            DaysBetween(fechaSolicitudTecnica, fechaAprobacion),
            DaysBetween(fechaAprobacion, fechaEnvio),
            DaysBetween(fechaEnvio, fechaOC),
            DaysBetween(fechaOC, fechaRecepcion),
            DaysBetween(fechaRecepcion, fechaEntrega),
            DaysBetween(fechaSolicitudTecnica, totalEnd));
    }

    private static int? DaysBetween(DateTimeOffset? start, DateTimeOffset? end)
    {
        return start.HasValue && end.HasValue
            ? (end.Value.UtcDateTime.Date - start.Value.UtcDateTime.Date).Days
            : null;
    }

    private static void ValidateSupplier(UpsertSupplierRequest request)
    {
        ValidateRequired(request.Rut, nameof(request.Rut));
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        if (request.LeadTimeEsperadoDias.GetValueOrDefault() < 0)
        {
            throw new DomainException("El lead time esperado no puede ser negativo.");
        }
    }

    private static void EnsurePositive(decimal value)
    {
        if (value <= 0)
        {
            throw new DomainException("La cantidad debe ser mayor a cero.");
        }
    }

    private static void EnsureCanView(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Warehouse, AuthRoles.WarehouseSupervisor, AuthRoles.Management, AuthRoles.FaenaViewer))
        {
            return;
        }

        throw new UnauthorizedAccessException("No tiene permisos para ver abastecimiento.");
    }

    private static void EnsureCanManage(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.WarehouseSupervisor) ||
            user.Permissions.Contains(AuthPermissions.AdjustStock, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw new UnauthorizedAccessException("La gestion de abastecimiento requiere supervisor de bodega.");
    }

    private static void EnsureFaenaAccess(UserAccessContext user, string? faenaCodigo)
    {
        if (!CanAccessFaena(user, faenaCodigo))
        {
            throw new UnauthorizedAccessException("No tiene acceso a la faena de la solicitud.");
        }
    }

    private static bool CanAccessFaena(UserAccessContext user, string? faenaCodigo)
    {
        return string.IsNullOrWhiteSpace(faenaCodigo)
            || HasAnyRole(user, AuthRoles.Admin, AuthRoles.Management, AuthRoles.Warehouse, AuthRoles.WarehouseSupervisor)
            || user.Permissions.Contains(AuthPermissions.ViewGlobalWarehouses, StringComparer.OrdinalIgnoreCase)
            || user.Faenas.Contains(faenaCodigo, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasAnyRole(UserAccessContext user, params string[] roles)
    {
        return roles.Any(role => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
    }

    private async Task RecordAuditAsync(
        UserAccessContext user,
        string action,
        string entityName,
        string entityId,
        DataRow? previous,
        DataRow? updated,
        string? faenaCodigo,
        string? reason,
        CancellationToken cancellationToken,
        AuditSeverity severity = AuditSeverity.Medium)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            AuditModules.Procurement,
            entityName,
            entityId,
            previous is null ? null : Serialize(previous),
            updated is null ? null : Serialize(updated),
            faenaCodigo,
            severity,
            reason),
            cancellationToken);
    }

    private static string NextRequestId(IReadOnlyCollection<DataRow> rows)
    {
        var next = rows
            .Select(row => row.GetValue("SolicitudId"))
            .Select(value => value is null ? 0 : int.TryParse(value.Replace("AB-", string.Empty, StringComparison.OrdinalIgnoreCase), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"AB-{next:000000}";
    }

    private static bool Contains(string? value, string? search)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !string.IsNullOrWhiteSpace(search) &&
               value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"El campo {fieldName} es obligatorio.");
        }
    }

    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string FormatDate(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string FormatNumber(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? FormatOptionalNumber(decimal? value) => value.HasValue ? FormatNumber(value.Value) : null;

    private static decimal ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static decimal? ParseOptionalDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    }

    private static bool ParseBool(string? value, bool fallback = false)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static string? Append(string? existing, string next)
    {
        return string.IsNullOrWhiteSpace(existing) ? next : $"{existing} | {next}";
    }

    private static string Serialize(DataRow row) => JsonSerializer.Serialize(row.Values);

    private static readonly string[] RequestColumns =
    [
        "SolicitudId",
        "Estado",
        "SolicitudInternaCmms",
        "SolicitudExternaNumero",
        "OcNumero",
        "ProveedorRut",
        "RepuestoCodigo",
        "Descripcion",
        "Cantidad",
        "Unidad",
        "CantidadRecibida",
        "CantidadEntregada",
        "FaenaCodigo",
        "BodegaCodigo",
        "OtNumero",
        "ActivoCodigo",
        "Motivo",
        "FechaSolicitudTecnica",
        "FechaAprobacionMantenimiento",
        "FechaEnvioAbastecimiento",
        "FechaOC",
        "FechaComprometida",
        "FechaRecepcion",
        "FechaEntrega",
        "CostoEstimado",
        "CostoOC",
        "CostoReal",
        "Moneda",
        "DocumentoRespaldoUrl",
        "DocumentoOcUrl",
        "DocumentoRecepcionUrl",
        "DocumentoEntregaUrl",
        "CreadoPor",
        "CreadoEnUtc",
        "ActualizadoPor",
        "ActualizadoEnUtc",
        "Observaciones"
    ];

    private static readonly string[] PurchaseOrderColumns =
    [
        "OrdenCompraId",
        "SolicitudId",
        "SolicitudExternaNumero",
        "OcNumero",
        "ProveedorRut",
        "FechaOC",
        "FechaComprometida",
        "CostoOC",
        "Moneda",
        "DocumentoOcUrl",
        "UsuarioId",
        "Motivo",
        "CreadoEnUtc"
    ];

    private static readonly string[] ReceiptColumns =
    [
        "RecepcionId",
        "SolicitudId",
        "OcNumero",
        "FechaRecepcion",
        "CantidadRecibida",
        "CantidadDespachada",
        "BodegaCodigo",
        "DespachoDirectoOt",
        "OtNumero",
        "ActivoCodigo",
        "FaenaCodigo",
        "MovimientoRecepcionId",
        "MovimientoEntregaId",
        "CostoReal",
        "DocumentoRecepcionUrl",
        "DocumentoEntregaUrl",
        "UsuarioId",
        "Motivo"
    ];
}
