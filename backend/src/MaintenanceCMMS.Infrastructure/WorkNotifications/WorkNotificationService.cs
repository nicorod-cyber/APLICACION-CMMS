using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;

namespace MaintenanceCMMS.Infrastructure.WorkNotifications;

public sealed class WorkNotificationService : IWorkNotificationService
{
    private const string NotificationsSchema = "avisos_trabajo";
    private const string AssetsSchema = "activos";
    private const string WorkOrdersSchema = "ordenes_trabajo";

    private readonly IDataProvider _dataProvider;
    private readonly IAuditService _auditService;

    public WorkNotificationService(
        IDataProvider dataProvider,
        IAuditService auditService)
    {
        _dataProvider = dataProvider;
        _auditService = auditService;
    }

    public async Task<IReadOnlyCollection<WorkNotificationResponse>> ListAsync(
        WorkNotificationQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);

        return (await _dataProvider.ReadRowsAsync(NotificationsSchema, cancellationToken))
            .Select(ToResponse)
            .Where(item => query.IncludeClosed || item.Estado is not (WorkNotificationStatus.Rechazado or WorkNotificationStatus.ConvertidoOT or WorkNotificationStatus.Anulado))
            .Where(item => !query.Status.HasValue || item.Estado == query.Status)
            .Where(item => !query.Type.HasValue || item.Tipo == query.Type)
            .Where(item => !query.Priority.HasValue || item.Prioridad == query.Priority)
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.ActivoCodigo) || Same(item.ActivoCodigo, query.ActivoCodigo))
            .Where(item => !query.SupervisorInbox || item.Estado is WorkNotificationStatus.Creado or WorkNotificationStatus.EnEvaluacion or WorkNotificationStatus.Aprobado)
            .Where(item => CanAccessFaena(user, item.FaenaCodigo))
            .OrderByDescending(item => item.Prioridad)
            .ThenByDescending(item => item.FechaDeteccion)
            .ToArray();
    }

    public async Task<WorkNotificationResponse?> GetByIdAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);

        var row = (await _dataProvider.ReadRowsAsync(NotificationsSchema, cancellationToken))
            .FirstOrDefault(item => Same(item.GetValue("AvisoId"), id));

        if (row is null)
        {
            return null;
        }

        var response = ToResponse(row);
        EnsureFaenaAccess(user, response.FaenaCodigo);
        return response;
    }

    public async Task<WorkNotificationResponse> CreateAsync(
        CreateWorkNotificationRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanCreate(user);
        ValidateCreate(request);

        var asset = await FindAssetAsync(request.ActivoCodigo, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ActivoCodigo) && asset is null)
        {
            throw new DomainException($"El activo '{request.ActivoCodigo}' no existe.");
        }

        var faenaCodigo = ResolveFaena(request.FaenaCodigo, asset);
        EnsureFaenaAccess(user, faenaCodigo);

        var rows = (await _dataProvider.ReadRowsAsync(NotificationsSchema, cancellationToken)).ToList();
        var id = NextNotificationNumber(rows);
        var now = DateTimeOffset.UtcNow;
        var row = NotificationRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["AvisoId"] = id,
            ["Estado"] = WorkNotificationStatus.Creado.ToString(),
            ["Tipo"] = request.Tipo.ToString(),
            ["FaenaCodigo"] = NormalizeCode(faenaCodigo),
            ["ActivoCodigo"] = NormalizeCode(request.ActivoCodigo),
            ["Sistema"] = NormalizeText(request.Sistema),
            ["Subsistema"] = NormalizeText(request.Subsistema),
            ["Componente"] = NormalizeText(request.Componente),
            ["Descripcion"] = NormalizeText(request.Descripcion),
            ["Prioridad"] = request.Prioridad.ToString(),
            ["Criticidad"] = request.Criticidad.ToString(),
            ["Solicitante"] = user.UserId,
            ["EvidenciaInicial"] = NormalizeText(request.EvidenciaInicial),
            ["FechaDeteccion"] = FormatDate(request.FechaDeteccion ?? now),
            ["FechaCreacion"] = FormatDate(now),
            ["ClasificacionFalla"] = request.ClasificacionFalla.ToString(),
            ["Observaciones"] = "Aviso creado en CMMS"
        });

        rows.Add(row);
        await _dataProvider.SaveRowsAsync(NotificationsSchema, rows, cancellationToken);
        await RecordNotificationAuditAsync(user, "work_notification.created", id, null, row, faenaCodigo, request.Descripcion, cancellationToken);

        return ToResponse(row);
    }

    public Task<WorkNotificationResponse?> EvaluateAsync(
        string id,
        WorkNotificationActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanEvaluate(user);
        ValidateReason(request.Reason);

        return UpdateNotificationAsync(id, user, cancellationToken, (current, values) =>
        {
            EnsureStatus(current, WorkNotificationStatus.Creado);
            values["Estado"] = WorkNotificationStatus.EnEvaluacion.ToString();
            values["EvaluadoPor"] = user.UserId;
            values["EvaluadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Evaluacion: {request.Reason}");
            return ("work_notification.evaluated", request.Reason);
        });
    }

    public Task<WorkNotificationResponse?> ApproveAsync(
        string id,
        WorkNotificationActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanEvaluate(user);
        ValidateReason(request.Reason);

        return UpdateNotificationAsync(id, user, cancellationToken, (current, values) =>
        {
            EnsureStatus(current, WorkNotificationStatus.Creado, WorkNotificationStatus.EnEvaluacion);
            values["Estado"] = WorkNotificationStatus.Aprobado.ToString();
            values["AprobadoPor"] = user.UserId;
            values["AprobadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Aprobacion: {request.Reason}");
            return ("work_notification.approved", request.Reason);
        });
    }

    public Task<WorkNotificationResponse?> RejectAsync(
        string id,
        WorkNotificationActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanEvaluate(user);
        ValidateReason(request.Reason);

        return UpdateNotificationAsync(id, user, cancellationToken, (current, values) =>
        {
            if (current.Estado is WorkNotificationStatus.ConvertidoOT or WorkNotificationStatus.Anulado)
            {
                throw new DomainException("El aviso ya no admite rechazo.");
            }

            values["Estado"] = WorkNotificationStatus.Rechazado.ToString();
            values["RechazadoPor"] = user.UserId;
            values["RechazadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["MotivoRechazo"] = request.Reason;
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Rechazo: {request.Reason}");
            return ("work_notification.rejected", request.Reason);
        });
    }

    public async Task<WorkNotificationConversionResponse?> ConvertToWorkOrderAsync(
        string id,
        ConvertWorkNotificationToWorkOrderRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanEvaluate(user);
        ValidateReason(request.Reason);

        var notificationRows = (await _dataProvider.ReadRowsAsync(NotificationsSchema, cancellationToken)).ToList();
        var index = notificationRows.FindIndex(item => Same(item.GetValue("AvisoId"), id));
        if (index < 0)
        {
            return null;
        }

        var previous = notificationRows[index];
        var current = ToResponse(previous);
        EnsureFaenaAccess(user, current.FaenaCodigo);
        EnsureStatus(current, WorkNotificationStatus.Aprobado);
        ValidateRequired(current.ActivoCodigo, "ActivoCodigo");

        var asset = await FindAssetAsync(current.ActivoCodigo, cancellationToken);
        if (asset is null)
        {
            throw new DomainException($"El activo '{current.ActivoCodigo}' no existe.");
        }

        var workOrderRows = (await _dataProvider.ReadRowsAsync(WorkOrdersSchema, cancellationToken)).ToList();
        var workOrderNumber = NextWorkOrderNumber(workOrderRows);
        var now = DateTimeOffset.UtcNow;
        var maintenanceType = NormalizeText(request.TipoMantenimiento) ?? ResolveMaintenanceType(current.Tipo);

        var workOrder = WorkOrderRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["NumeroOT"] = workOrderNumber,
            ["ActivoCodigo"] = NormalizeCode(current.ActivoCodigo),
            ["Estado"] = "OTCreada",
            ["TipoMantenimiento"] = maintenanceType,
            ["Descripcion"] = current.Descripcion,
            ["FechaProgramada"] = request.FechaProgramada.HasValue ? FormatDate(request.FechaProgramada.Value) : null,
            ["AvisoId"] = current.AvisoId,
            ["FaenaCodigo"] = current.FaenaCodigo,
            ["Prioridad"] = current.Prioridad.ToString(),
            ["Criticidad"] = current.Criticidad.ToString(),
            ["ClasificacionFalla"] = current.ClasificacionFalla.ToString(),
            ["CreadoPor"] = user.UserId,
            ["CreadoEnUtc"] = FormatDate(now)
        });
        workOrderRows.Add(workOrder);

        var values = CopyValues(previous);
        values["Estado"] = WorkNotificationStatus.ConvertidoOT.ToString();
        values["NumeroOT"] = workOrderNumber;
        values["ConvertidoPor"] = user.UserId;
        values["ConvertidoEnUtc"] = FormatDate(now);
        values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"OT {workOrderNumber}: {request.Reason}");
        var updated = NotificationRow(values);
        notificationRows[index] = updated;

        await _dataProvider.SaveRowsAsync(WorkOrdersSchema, workOrderRows, cancellationToken);
        await _dataProvider.SaveRowsAsync(NotificationsSchema, notificationRows, cancellationToken);
        await RecordNotificationAuditAsync(user, "work_notification.converted_to_work_order", current.AvisoId, previous, updated, current.FaenaCodigo, request.Reason, cancellationToken, AuditSeverity.High);
        await RecordWorkOrderAuditAsync(user, "work_order.created_from_notification", workOrderNumber, null, workOrder, current.FaenaCodigo, current.AvisoId, cancellationToken);

        return new WorkNotificationConversionResponse(ToResponse(updated), workOrderNumber);
    }

    public Task<WorkNotificationResponse?> AnnulAsync(
        string id,
        WorkNotificationActionRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanEvaluate(user);
        ValidateReason(request.Reason);

        return UpdateNotificationAsync(id, user, cancellationToken, (current, values) =>
        {
            if (current.Estado == WorkNotificationStatus.ConvertidoOT)
            {
                throw new DomainException("No se puede anular un aviso convertido a OT.");
            }

            values["Estado"] = WorkNotificationStatus.Anulado.ToString();
            values["AnuladoPor"] = user.UserId;
            values["AnuladoEnUtc"] = FormatDate(DateTimeOffset.UtcNow);
            values["MotivoAnulacion"] = request.Reason;
            values["Observaciones"] = Append(values.GetValueOrDefault("Observaciones"), $"Anulacion: {request.Reason}");
            return ("work_notification.annulled", request.Reason);
        });
    }

    private async Task<WorkNotificationResponse?> UpdateNotificationAsync(
        string id,
        UserAccessContext user,
        CancellationToken cancellationToken,
        Func<WorkNotificationResponse, Dictionary<string, string?>, (string Action, string? Reason)> mutate)
    {
        var rows = (await _dataProvider.ReadRowsAsync(NotificationsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(item => Same(item.GetValue("AvisoId"), id));
        if (index < 0)
        {
            return null;
        }

        var previous = rows[index];
        var current = ToResponse(previous);
        EnsureFaenaAccess(user, current.FaenaCodigo);
        var values = CopyValues(previous);
        var (action, reason) = mutate(current, values);
        var updated = NotificationRow(values);
        rows[index] = updated;
        await _dataProvider.SaveRowsAsync(NotificationsSchema, rows, cancellationToken);
        await RecordNotificationAuditAsync(user, action, current.AvisoId, previous, updated, current.FaenaCodigo, reason, cancellationToken);
        return ToResponse(updated);
    }

    private async Task<DataRow?> FindAssetAsync(string? assetCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetCode))
        {
            return null;
        }

        return (await _dataProvider.ReadRowsAsync(AssetsSchema, cancellationToken))
            .FirstOrDefault(row => Same(row.GetValue("Codigo"), assetCode));
    }

    private static string ResolveFaena(string? requestedFaena, DataRow? asset)
    {
        var assetFaena = asset?.GetValue("FaenaCodigo");
        if (asset is null && !string.IsNullOrWhiteSpace(requestedFaena))
        {
            return NormalizeCode(requestedFaena) ?? string.Empty;
        }

        if (asset is not null && string.IsNullOrWhiteSpace(requestedFaena))
        {
            return NormalizeCode(assetFaena) ?? string.Empty;
        }

        if (asset is not null && !Same(requestedFaena, assetFaena))
        {
            throw new DomainException("El activo seleccionado no pertenece a la faena indicada.");
        }

        return NormalizeCode(requestedFaena) ?? string.Empty;
    }

    private static WorkNotificationResponse ToResponse(DataRow row)
    {
        return new WorkNotificationResponse(
            row.GetValue("AvisoId") ?? string.Empty,
            ParseEnum(row.GetValue("Estado"), WorkNotificationStatus.Creado),
            ParseEnum(row.GetValue("Tipo"), WorkNotificationType.Falla),
            row.GetValue("FaenaCodigo") ?? string.Empty,
            EmptyToNull(row.GetValue("ActivoCodigo")),
            EmptyToNull(row.GetValue("Sistema")),
            EmptyToNull(row.GetValue("Subsistema")),
            EmptyToNull(row.GetValue("Componente")),
            row.GetValue("Descripcion") ?? string.Empty,
            ParseEnum(row.GetValue("Prioridad"), WorkNotificationPriority.Media),
            ParseEnum(row.GetValue("Criticidad"), WorkNotificationCriticality.Media),
            row.GetValue("Solicitante") ?? string.Empty,
            EmptyToNull(row.GetValue("EvidenciaInicial")),
            ParseDate(row.GetValue("FechaDeteccion")) ?? DateTimeOffset.MinValue,
            ParseDate(row.GetValue("FechaCreacion")) ?? DateTimeOffset.MinValue,
            ParseEnum(row.GetValue("ClasificacionFalla"), WorkFailureClassification.SinDetencion),
            EmptyToNull(row.GetValue("EvaluadoPor")),
            ParseDate(row.GetValue("EvaluadoEnUtc")),
            EmptyToNull(row.GetValue("AprobadoPor")),
            ParseDate(row.GetValue("AprobadoEnUtc")),
            EmptyToNull(row.GetValue("RechazadoPor")),
            ParseDate(row.GetValue("RechazadoEnUtc")),
            EmptyToNull(row.GetValue("MotivoRechazo")),
            EmptyToNull(row.GetValue("AnuladoPor")),
            ParseDate(row.GetValue("AnuladoEnUtc")),
            EmptyToNull(row.GetValue("MotivoAnulacion")),
            EmptyToNull(row.GetValue("NumeroOT")),
            EmptyToNull(row.GetValue("ConvertidoPor")),
            ParseDate(row.GetValue("ConvertidoEnUtc")),
            EmptyToNull(row.GetValue("Observaciones")));
    }

    private static DataRow NotificationRow(IReadOnlyDictionary<string, string?> values) => Row(NotificationColumns, values);

    private static DataRow WorkOrderRow(IReadOnlyDictionary<string, string?> values) => Row(WorkOrderColumns, values);

    private static DataRow Row(IEnumerable<string> columns, IReadOnlyDictionary<string, string?> values)
    {
        return new DataRow(columns.ToDictionary(
            column => column,
            column => values.TryGetValue(column, out var value) ? value : null,
            StringComparer.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string?> CopyValues(DataRow row)
    {
        return NotificationColumns.ToDictionary(column => column, column => row.GetValue(column), StringComparer.OrdinalIgnoreCase);
    }

    private static string NextNotificationNumber(IReadOnlyCollection<DataRow> rows)
    {
        var next = rows
            .Select(row => row.GetValue("AvisoId"))
            .Select(value => ParseNumberSuffix(value, "AV-"))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"AV-{next:000000}";
    }

    private static string NextWorkOrderNumber(IReadOnlyCollection<DataRow> rows)
    {
        var next = rows
            .Select(row => row.GetValue("NumeroOT"))
            .Select(value => ParseNumberSuffix(value, "OT-"))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"OT-{next:000000}";
    }

    private static int ParseNumberSuffix(string? value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var normalized = value.Replace(prefix, string.Empty, StringComparison.OrdinalIgnoreCase);
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static string ResolveMaintenanceType(WorkNotificationType type)
    {
        return type switch
        {
            WorkNotificationType.Preventivo => MaintenanceType.Preventive.ToString(),
            WorkNotificationType.Inspeccion => MaintenanceType.Inspection.ToString(),
            WorkNotificationType.Mejora => MaintenanceType.Predictive.ToString(),
            _ => MaintenanceType.Corrective.ToString()
        };
    }

    private static void ValidateCreate(CreateWorkNotificationRequest request)
    {
        ValidateRequired(request.Descripcion, nameof(request.Descripcion));
        if (string.IsNullOrWhiteSpace(request.FaenaCodigo) && string.IsNullOrWhiteSpace(request.ActivoCodigo))
        {
            throw new DomainException("Debe indicar faena o activo para crear el aviso.");
        }

        if (request.FechaDeteccion.HasValue && request.FechaDeteccion.Value > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            throw new DomainException("La fecha de deteccion no puede ser futura.");
        }
    }

    private static void EnsureStatus(WorkNotificationResponse request, params WorkNotificationStatus[] expected)
    {
        if (!expected.Contains(request.Estado))
        {
            throw new DomainException($"El aviso esta en estado {request.Estado} y no admite esta accion.");
        }
    }

    private static void EnsureCanView(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Technician, AuthRoles.Management, AuthRoles.FaenaViewer))
        {
            return;
        }

        throw new UnauthorizedAccessException("No tiene permisos para ver avisos de trabajo.");
    }

    private static void EnsureCanCreate(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Technician))
        {
            return;
        }

        throw new UnauthorizedAccessException("No tiene permisos para crear avisos de trabajo.");
    }

    private static void EnsureCanEvaluate(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor))
        {
            return;
        }

        throw new UnauthorizedAccessException("La evaluacion de avisos requiere planificador o supervisor de mantenimiento.");
    }

    private static void EnsureFaenaAccess(UserAccessContext user, string? faenaCodigo)
    {
        if (!CanAccessFaena(user, faenaCodigo))
        {
            throw new UnauthorizedAccessException("No tiene acceso a la faena del aviso.");
        }
    }

    private static bool CanAccessFaena(UserAccessContext user, string? faenaCodigo)
    {
        return string.IsNullOrWhiteSpace(faenaCodigo)
            || HasAnyRole(user, AuthRoles.Admin, AuthRoles.Management)
            || user.Faenas.Contains(faenaCodigo, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasAnyRole(UserAccessContext user, params string[] roles)
    {
        return roles.Any(role => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
    }

    private async Task RecordNotificationAuditAsync(
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
            AuditModules.WorkNotifications,
            "WorkNotification",
            entityId,
            previous is null ? null : Serialize(previous),
            updated is null ? null : Serialize(updated),
            faenaCodigo,
            severity,
            reason),
            cancellationToken);
    }

    private async Task RecordWorkOrderAuditAsync(
        UserAccessContext user,
        string action,
        string entityId,
        DataRow? previous,
        DataRow? updated,
        string? faenaCodigo,
        string? reason,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            AuditModules.WorkOrders,
            "WorkOrder",
            entityId,
            previous is null ? null : Serialize(previous),
            updated is null ? null : Serialize(updated),
            faenaCodigo,
            AuditSeverity.High,
            reason),
            cancellationToken);
    }

    private static void ValidateReason(string? value)
    {
        ValidateRequired(value, "Reason");
    }

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

    private static string FormatDate(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

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

    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? Append(string? existing, string next)
    {
        return string.IsNullOrWhiteSpace(existing) ? next : $"{existing} | {next}";
    }

    private static string Serialize(DataRow row) => JsonSerializer.Serialize(row.Values);

    private static readonly string[] NotificationColumns =
    [
        "AvisoId",
        "Estado",
        "Tipo",
        "FaenaCodigo",
        "ActivoCodigo",
        "Sistema",
        "Subsistema",
        "Componente",
        "Descripcion",
        "Prioridad",
        "Criticidad",
        "Solicitante",
        "EvidenciaInicial",
        "FechaDeteccion",
        "FechaCreacion",
        "ClasificacionFalla",
        "EvaluadoPor",
        "EvaluadoEnUtc",
        "AprobadoPor",
        "AprobadoEnUtc",
        "RechazadoPor",
        "RechazadoEnUtc",
        "MotivoRechazo",
        "AnuladoPor",
        "AnuladoEnUtc",
        "MotivoAnulacion",
        "NumeroOT",
        "ConvertidoPor",
        "ConvertidoEnUtc",
        "Observaciones"
    ];

    private static readonly string[] WorkOrderColumns =
    [
        "NumeroOT",
        "ActivoCodigo",
        "Estado",
        "TipoMantenimiento",
        "Descripcion",
        "FechaProgramada",
        "AvisoId",
        "FaenaCodigo",
        "Prioridad",
        "Criticidad",
        "ClasificacionFalla",
        "CreadoPor",
        "CreadoEnUtc"
    ];
}
