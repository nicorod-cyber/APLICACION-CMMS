using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.MaterialRequests;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Infrastructure.MaterialRequests;

public sealed class MaterialRequestService : IMaterialRequestService
{
    private const string RequestsSchema = "solicitudes_repuestos";

    private readonly IDataProvider _dataProvider;
    private readonly IInventoryService _inventoryService;
    private readonly IAuditService _auditService;

    public MaterialRequestService(
        IDataProvider dataProvider,
        IInventoryService inventoryService,
        IAuditService auditService)
    {
        _dataProvider = dataProvider;
        _inventoryService = inventoryService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyCollection<MaterialRequestResponse>> ListAsync(
        MaterialRequestQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);

        return (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken))
            .Select(ToResponse)
            .Where(item => query.IncludeClosed || (item.Estado != MaterialRequestStatus.Cerrada && item.Estado != MaterialRequestStatus.Rechazada))
            .Where(item => !query.Status.HasValue || item.Estado == query.Status)
            .Where(item => !query.Type.HasValue || item.Tipo == query.Type)
            .Where(item => !query.Source.HasValue || item.Origen == query.Source)
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.Requester) || Same(item.Solicitante, query.Requester))
            .Where(item => CanAccessFaena(user, item.FaenaCodigo))
            .OrderByDescending(item => item.SolicitadoEnUtc)
            .ToArray();
    }

    public async Task<MaterialRequestResponse?> GetByIdAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var row = (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken))
            .FirstOrDefault(item => Same(item.GetValue("NumeroSolicitud"), id));
        if (row is null)
        {
            return null;
        }

        var response = ToResponse(row);
        EnsureFaenaAccess(user, response.FaenaCodigo);
        return response;
    }

    public async Task<MaterialRequestResponse> CreateAsync(
        CreateMaterialRequestRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanRequest(user);
        ValidateCreate(request);
        EnsureFaenaAccess(user, request.FaenaCodigo);

        var rows = (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken)).ToList();
        var number = NextRequestNumber(rows);
        var now = DateTimeOffset.UtcNow;
        var row = RequestRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["NumeroSolicitud"] = number,
            ["Tipo"] = request.Type.ToString(),
            ["Origen"] = request.Source.ToString(),
            ["Estado"] = MaterialRequestStatus.PendienteAprobacionMantenimiento.ToString(),
            ["Solicitante"] = user.UserId,
            ["SolicitadoEnUtc"] = FormatDate(now),
            ["OT"] = NormalizeText(request.OtNumero),
            ["TareaCodigo"] = NormalizeText(request.TareaCodigo),
            ["ActivoCodigo"] = NormalizeText(request.ActivoCodigo),
            ["FaenaCodigo"] = NormalizeText(request.FaenaCodigo),
            ["BodegaCodigo"] = NormalizeText(request.BodegaCodigo),
            ["RepuestoCodigo"] = NormalizeCode(request.RepuestoCodigo),
            ["DescripcionTecnica"] = NormalizeText(request.DescripcionTecnica),
            ["Cantidad"] = FormatNumber(request.Cantidad),
            ["Unidad"] = NormalizeText(request.Unidad),
            ["FotoReferencia"] = NormalizeText(request.FotoReferencia),
            ["Motivo"] = NormalizeText(request.Motivo),
            ["StockDecision"] = "Pendiente de aprobacion de mantenimiento"
        });

        rows.Add(row);
        await _dataProvider.SaveRowsAsync(RequestsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "material_request.created", number, null, row, request.FaenaCodigo, request.Motivo, cancellationToken);

        return ToResponse(row);
    }

    public async Task<MaterialRequestResponse?> ApproveMaintenanceAsync(
        string id,
        MaterialRequestReasonRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureMaintenanceApproval(user);
        ValidateReason(request.Reason);
        return await UpdateAsync(id, user, cancellationToken, (current, values) =>
        {
            EnsureStatus(current, MaterialRequestStatus.PendienteAprobacionMantenimiento);
            values["Estado"] = MaterialRequestStatus.AprobadaPorMantenimiento.ToString();
            values["AprobadorMantenimiento"] = user.UserId;
            values["AprobadoMantenimientoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Mantenimiento: {request.Reason}");
            values["StockDecision"] = "Aprobada por mantenimiento; pendiente revision bodega";
            return ("material_request.maintenance_approved", request.Reason);
        });
    }

    public async Task<MaterialRequestResponse?> RejectAsync(
        string id,
        MaterialRequestReasonRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanReject(user);
        ValidateReason(request.Reason);
        return await UpdateAsync(id, user, cancellationToken, (current, values) =>
        {
            if (current.Estado is MaterialRequestStatus.Cerrada or MaterialRequestStatus.Entregada or MaterialRequestStatus.RecibidaPorTecnico)
            {
                throw new DomainException("La solicitud ya no admite rechazo.");
            }

            values["Estado"] = MaterialRequestStatus.Rechazada.ToString();
            values["RechazadoPor"] = user.UserId;
            values["RechazadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["MotivoRechazo"] = request.Reason;
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Rechazo: {request.Reason}");
            return ("material_request.rejected", request.Reason);
        });
    }

    public async Task<MaterialRequestResponse?> ReviewWarehouseAsync(
        string id,
        WarehouseReviewMaterialRequestRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureWarehouseApproval(user);
        ValidateRequired(request.BodegaCodigo, nameof(request.BodegaCodigo));
        ValidateReason(request.Reason);

        var rows = (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(item => Same(item.GetValue("NumeroSolicitud"), id));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var current = ToResponse(previous);
        EnsureFaenaAccess(user, current.FaenaCodigo);
        EnsureStatus(current, MaterialRequestStatus.AprobadaPorMantenimiento, MaterialRequestStatus.EnRevisionBodega);

        var values = CopyValues(previous);
        values["Estado"] = MaterialRequestStatus.EnRevisionBodega.ToString();
        values["AprobadorBodega"] = user.UserId;
        values["AprobadoBodegaEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
        values["BodegaCodigo"] = NormalizeCode(request.BodegaCodigo);
        values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Bodega: {request.Reason}");

        if (current.Tipo == MaterialRequestType.MaterialNoCodificado)
        {
            values["Estado"] = MaterialRequestStatus.PendienteAbastecimiento.ToString();
            values["StockDecision"] = "Material no codificado derivado a abastecimiento";
        }
        else
        {
            var stock = await _inventoryService.ListStockAsync(
                new StockQuery(BodegaCodigo: request.BodegaCodigo, RepuestoCodigo: current.RepuestoCodigo),
                user,
                cancellationToken);
            var selected = stock.FirstOrDefault(item => item.StockDisponible >= current.Cantidad);
            if (selected is not null)
            {
                var reservation = await _inventoryService.CreateReservationAsync(
                    new CreateStockReservationRequest(
                        selected.RepuestoCodigo,
                        selected.BodegaCodigo,
                        current.Cantidad,
                        string.IsNullOrWhiteSpace(current.OtNumero) ? $"SOL-{current.NumeroSolicitud}" : current.OtNumero,
                        current.Solicitante,
                        $"Solicitud {current.NumeroSolicitud}: {request.Reason}"),
                    user,
                    cancellationToken);

                values["Estado"] = MaterialRequestStatus.Reservada.ToString();
                values["ReservaId"] = reservation.ReservaId;
                values["BodegaCodigo"] = selected.BodegaCodigo;
                values["StockDecision"] = "Reserva automatica generada";
            }
            else if (stock.Any(item => item.StockDisponible > 0))
            {
                values["Estado"] = MaterialRequestStatus.PendienteStock.ToString();
                values["StockDecision"] = "Stock insuficiente en bodega";
            }
            else
            {
                values["Estado"] = MaterialRequestStatus.PendienteAbastecimiento.ToString();
                values["StockDecision"] = "Sin stock disponible; derivado a abastecimiento";
            }
        }

        var updated = RequestRow(values);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(RequestsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "material_request.warehouse_reviewed", current.NumeroSolicitud, previous, updated, current.FaenaCodigo, request.Reason, cancellationToken);
        return ToResponse(updated);
    }

    public Task<MaterialRequestResponse?> PrepareAsync(
        string id,
        MaterialRequestReasonRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureWarehouseWork(user);
        ValidateReason(request.Reason);
        return UpdateAsync(id, user, cancellationToken, (current, values) =>
        {
            EnsureStatus(current, MaterialRequestStatus.Reservada, MaterialRequestStatus.PendienteStock, MaterialRequestStatus.PendienteAbastecimiento);
            values["Estado"] = MaterialRequestStatus.EnPreparacion.ToString();
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Preparacion: {request.Reason}");
            return ("material_request.prepared", request.Reason);
        });
    }

    public async Task<MaterialRequestResponse?> DeliverAsync(
        string id,
        DeliverRequestedMaterialRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureWarehouseWork(user);
        ValidateRequired(request.BodegaCodigo, nameof(request.BodegaCodigo));
        ValidateReason(request.Reason);

        var rows = (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(item => Same(item.GetValue("NumeroSolicitud"), id));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var current = ToResponse(previous);
        EnsureFaenaAccess(user, current.FaenaCodigo);
        EnsureStatus(current, MaterialRequestStatus.Reservada, MaterialRequestStatus.EnPreparacion);
        var values = CopyValues(previous);

        if (current.Tipo == MaterialRequestType.RepuestoCodificado)
        {
            var movement = await _inventoryService.DeliverMaterialAsync(
                new DeliverMaterialRequest(
                    current.RepuestoCodigo ?? throw new DomainException("La solicitud no tiene repuesto codificado."),
                    request.BodegaCodigo,
                    current.Cantidad,
                    $"Solicitud {current.NumeroSolicitud}: {request.Reason}",
                    WorkOrderId: string.IsNullOrWhiteSpace(current.OtNumero) ? null : current.OtNumero,
                    AssetCode: current.ActivoCodigo,
                    FaenaCodigo: current.FaenaCodigo,
                    ReservationId: current.ReservaId),
                user,
                cancellationToken);
            values["MovimientoEntregaId"] = movement.MovimientoId;
        }

        values["Estado"] = MaterialRequestStatus.Entregada.ToString();
        values["BodegaCodigo"] = NormalizeCode(request.BodegaCodigo);
        values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Entrega: {request.Reason}");
        var updated = RequestRow(values);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(RequestsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "material_request.delivered", current.NumeroSolicitud, previous, updated, current.FaenaCodigo, request.Reason, cancellationToken, AuditSeverity.High);
        return ToResponse(updated);
    }

    public Task<MaterialRequestResponse?> ReceiveAsync(
        string id,
        MaterialRequestReasonRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanRequest(user);
        ValidateReason(request.Reason);
        return UpdateAsync(id, user, cancellationToken, (current, values) =>
        {
            EnsureStatus(current, MaterialRequestStatus.Entregada);
            values["Estado"] = MaterialRequestStatus.RecibidaPorTecnico.ToString();
            values["RecibidoPor"] = user.UserId;
            values["RecibidoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Recepcion tecnico: {request.Reason}");
            return ("material_request.received", request.Reason);
        });
    }

    public Task<MaterialRequestResponse?> CloseAsync(
        string id,
        MaterialRequestReasonRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureMaintenanceApproval(user);
        ValidateReason(request.Reason);
        return UpdateAsync(id, user, cancellationToken, (current, values) =>
        {
            EnsureStatus(current, MaterialRequestStatus.RecibidaPorTecnico);
            values["Estado"] = MaterialRequestStatus.Cerrada.ToString();
            values["CerradoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Cierre: {request.Reason}");
            return ("material_request.closed", request.Reason);
        });
    }

    public async Task<MaterialRequestResponse?> ConvertToSparePartAsync(
        string id,
        ConvertMaterialRequestToSparePartRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureWarehouseApproval(user);
        ValidateRequired(request.Descripcion, nameof(request.Descripcion));
        ValidateRequired(request.UnidadMedida, nameof(request.UnidadMedida));

        var rows = (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(item => Same(item.GetValue("NumeroSolicitud"), id));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var current = ToResponse(previous);
        EnsureFaenaAccess(user, current.FaenaCodigo);
        if (current.Tipo != MaterialRequestType.MaterialNoCodificado)
        {
            throw new DomainException("Solo un material no codificado puede convertirse a repuesto maestro.");
        }

        if (!string.IsNullOrWhiteSpace(current.RepuestoMaestroCodigo))
        {
            throw new DomainException("El material no codificado ya fue convertido a repuesto maestro.");
        }

        if (current.AprobadoMantenimientoEnUtc is null || string.IsNullOrWhiteSpace(current.AprobadorBodega))
        {
            throw new DomainException("La conversion a repuesto maestro requiere aprobacion de mantenimiento y revision de bodega.");
        }

        var created = await _inventoryService.CreateSparePartAsync(
            new CreateSparePartRequest(
                request.Descripcion,
                request.UnidadMedida,
                request.CodigoSap,
                request.CodigoProveedor,
                request.DescripcionTecnica ?? current.DescripcionTecnica,
                request.FamiliaEquipo,
                request.MarcaFabricante,
                request.ModeloReferencia,
                request.Critico,
                request.StockMinimo,
                request.StockMaximo,
                request.PuntoReposicion,
                request.LeadTimeEsperadoDias,
                Estado: SparePartStatus.Activo,
                ProveedorPreferente: request.ProveedorPreferente),
            user,
            cancellationToken);

        var values = CopyValues(previous);
        values["RepuestoMaestroCodigo"] = created.Summary.Codigo;
        values["RepuestoCodigo"] = created.Summary.Codigo;
        values["ConvertidoPor"] = user.UserId;
        values["ConvertidoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
        values["StockDecision"] = "Convertido a repuesto maestro; pendiente abastecimiento";
        values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Conversion a maestro {created.Summary.Codigo}");

        var updated = RequestRow(values);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(RequestsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "material_request.converted_to_spare_part", current.NumeroSolicitud, previous, updated, current.FaenaCodigo, created.Summary.Codigo, cancellationToken, AuditSeverity.High);
        return ToResponse(updated);
    }

    private async Task<MaterialRequestResponse?> UpdateAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken,
        Func<MaterialRequestResponse, Dictionary<string, string?>, (string Action, string? Reason)> mutate)
    {
        var rows = (await _dataProvider.ReadRowsAsync(RequestsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(item => Same(item.GetValue("NumeroSolicitud"), id));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var current = ToResponse(previous);
        EnsureFaenaAccess(user, current.FaenaCodigo);
        var values = CopyValues(previous);
        var (action, reason) = mutate(current, values);
        var updated = RequestRow(values);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(RequestsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, action, current.NumeroSolicitud, previous, updated, current.FaenaCodigo, reason, cancellationToken);
        return ToResponse(updated);
    }

    private static void ValidateCreate(CreateMaterialRequestRequest request)
    {
        ValidateRequired(request.DescripcionTecnica, nameof(request.DescripcionTecnica));
        ValidateRequired(request.Unidad, nameof(request.Unidad));
        ValidateRequired(request.Motivo, nameof(request.Motivo));
        if (request.Cantidad <= 0)
        {
            throw new DomainException("La cantidad solicitada debe ser mayor a cero.");
        }

        if (request.Type == MaterialRequestType.RepuestoCodificado)
        {
            ValidateRequired(request.RepuestoCodigo, nameof(request.RepuestoCodigo));
        }

        if (request.Source is MaterialRequestSource.OT or MaterialRequestSource.Tarea)
        {
            ValidateRequired(request.OtNumero, nameof(request.OtNumero));
        }

        if (request.Source == MaterialRequestSource.Tarea)
        {
            ValidateRequired(request.TareaCodigo, nameof(request.TareaCodigo));
        }
    }

    private static MaterialRequestResponse ToResponse(DataRow row)
    {
        return new MaterialRequestResponse(
            row.GetValue("NumeroSolicitud") ?? string.Empty,
            ParseEnum(row.GetValue("Estado"), MaterialRequestStatus.PendienteAprobacionMantenimiento),
            ParseEnum(row.GetValue("Tipo"), MaterialRequestType.RepuestoCodificado),
            ParseEnum(row.GetValue("Origen"), MaterialRequestSource.Bodega),
            row.GetValue("Solicitante") ?? string.Empty,
            ParseDate(row.GetValue("SolicitadoEnUtc")) ?? DateTimeOffset.MinValue,
            row.GetValue("DescripcionTecnica") ?? string.Empty,
            ParseDecimal(row.GetValue("Cantidad")),
            row.GetValue("Unidad") ?? string.Empty,
            row.GetValue("Motivo") ?? string.Empty,
            EmptyToNull(row.GetValue("RepuestoCodigo")),
            EmptyToNull(row.GetValue("RepuestoMaestroCodigo")),
            EmptyToNull(row.GetValue("FotoReferencia")),
            EmptyToNull(row.GetValue("ActivoCodigo")),
            EmptyToNull(row.GetValue("OT")),
            EmptyToNull(row.GetValue("TareaCodigo")),
            EmptyToNull(row.GetValue("FaenaCodigo")),
            EmptyToNull(row.GetValue("BodegaCodigo")),
            EmptyToNull(row.GetValue("ReservaId")),
            EmptyToNull(row.GetValue("MovimientoEntregaId")),
            EmptyToNull(row.GetValue("StockDecision")),
            EmptyToNull(row.GetValue("AprobadorMantenimiento")),
            ParseDate(row.GetValue("AprobadoMantenimientoEnUtc")),
            EmptyToNull(row.GetValue("AprobadorBodega")),
            ParseDate(row.GetValue("AprobadoBodegaEnUtc")),
            EmptyToNull(row.GetValue("RechazadoPor")),
            ParseDate(row.GetValue("RechazadoEnUtc")),
            EmptyToNull(row.GetValue("MotivoRechazo")),
            EmptyToNull(row.GetValue("RecibidoPor")),
            ParseDate(row.GetValue("RecibidoEnUtc")),
            EmptyToNull(row.GetValue("ConvertidoPor")),
            ParseDate(row.GetValue("ConvertidoEnUtc")),
            ParseDate(row.GetValue("CerradoEnUtc")),
            EmptyToNull(row.GetValue("Observaciones")));
    }

    private static DataRow RequestRow(IReadOnlyDictionary<string, string?> values)
    {
        var all = RequestColumns.ToDictionary(column => column, column => values.TryGetValue(column, out var value) ? value : null, StringComparer.OrdinalIgnoreCase);
        return new DataRow(all);
    }

    private static Dictionary<string, string?> CopyValues(DataRow row)
    {
        return RequestColumns.ToDictionary(column => column, column => row.GetValue(column), StringComparer.OrdinalIgnoreCase);
    }

    private static string NextRequestNumber(IReadOnlyCollection<DataRow> rows)
    {
        var next = rows
            .Select(row => row.GetValue("NumeroSolicitud"))
            .Select(value => value is null ? 0 : int.TryParse(value.Replace("SOL-", string.Empty, StringComparison.OrdinalIgnoreCase), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"SOL-{next:000000}";
    }

    private async Task RecordAuditAsync(
        UserAccessContext user,
        string action,
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
            AuditModules.MaterialRequests,
            "MaterialRequest",
            entityId,
            previous is null ? null : Serialize(previous),
            updated is null ? null : Serialize(updated),
            faenaCodigo,
            severity,
            reason),
            cancellationToken);
    }

    private static void EnsureStatus(MaterialRequestResponse request, params MaterialRequestStatus[] expected)
    {
        if (!expected.Contains(request.Estado))
        {
            throw new DomainException($"La solicitud esta en estado {request.Estado} y no admite esta accion.");
        }
    }

    private static void EnsureCanView(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Technician, AuthRoles.Warehouse, AuthRoles.WarehouseSupervisor, AuthRoles.Management, AuthRoles.FaenaViewer))
        {
            return;
        }

        throw new UnauthorizedAccessException("No tiene permisos para ver solicitudes de repuestos.");
    }

    private static void EnsureCanRequest(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Technician, AuthRoles.Warehouse, AuthRoles.WarehouseSupervisor))
        {
            return;
        }

        throw new UnauthorizedAccessException("No tiene permisos para crear o recibir solicitudes.");
    }

    private static void EnsureMaintenanceApproval(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor))
        {
            return;
        }

        throw new UnauthorizedAccessException("La aprobacion de mantenimiento requiere supervisor de mantenimiento.");
    }

    private static void EnsureWarehouseApproval(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.WarehouseSupervisor) || user.Permissions.Contains(AuthPermissions.AdjustStock, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw new UnauthorizedAccessException("La revision de bodega requiere supervisor de bodega.");
    }

    private static void EnsureWarehouseWork(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.WarehouseSupervisor, AuthRoles.Warehouse) || user.Permissions.Contains(AuthPermissions.AdjustStock, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw new UnauthorizedAccessException("La preparacion y entrega requiere rol de bodega.");
    }

    private static void EnsureCanReject(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.WarehouseSupervisor))
        {
            return;
        }

        throw new UnauthorizedAccessException("No tiene permisos para rechazar solicitudes.");
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
            || HasAnyRole(user, AuthRoles.Admin, AuthRoles.Management)
            || user.Permissions.Contains(AuthPermissions.ViewGlobalWarehouses, StringComparer.OrdinalIgnoreCase)
            || user.Faenas.Contains(faenaCodigo, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasAnyRole(UserAccessContext user, params string[] roles)
    {
        return roles.Any(role => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
    }

    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"El campo {fieldName} es obligatorio.");
        }
    }

    private static void ValidateReason(string? reason) => ValidateRequired(reason, "Reason");

    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string FormatDate(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string FormatNumber(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
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
        "NumeroSolicitud",
        "Tipo",
        "Origen",
        "Estado",
        "Solicitante",
        "SolicitadoEnUtc",
        "AprobadorMantenimiento",
        "AprobadoMantenimientoEnUtc",
        "AprobadorBodega",
        "AprobadoBodegaEnUtc",
        "RechazadoPor",
        "RechazadoEnUtc",
        "MotivoRechazo",
        "OT",
        "TareaCodigo",
        "ActivoCodigo",
        "FaenaCodigo",
        "BodegaCodigo",
        "RepuestoCodigo",
        "RepuestoMaestroCodigo",
        "DescripcionTecnica",
        "Cantidad",
        "Unidad",
        "FotoReferencia",
        "Motivo",
        "ReservaId",
        "MovimientoEntregaId",
        "StockDecision",
        "CerradoEnUtc",
        "RecibidoPor",
        "RecibidoEnUtc",
        "ConvertidoPor",
        "ConvertidoEnUtc",
        "Observaciones"
    ];
}
