using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Scheduling;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Infrastructure.Scheduling;

public sealed class SchedulingService : ISchedulingService
{
    private const string WorkshopsSchema = "programacion_talleres";
    private const string ScheduleSchema = "programacion_ot";
    private const string DependenciesSchema = "programacion_dependencias";
    private const string AlertsSchema = "programacion_alertas";
    private const string WorkOrdersSchema = "ordenes_trabajo";
    private const string AssetsSchema = "activos";

    private readonly CmmsDbContext _dbContext;
    private readonly IWorkOrderService _workOrderService;
    private readonly IAuditService _auditService;

    public SchedulingService(
        CmmsDbContext dbContext,
        IWorkOrderService workOrderService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _workOrderService = workOrderService;
        _auditService = auditService;
    }

    public async Task<ScheduleBoardResponse> GetBoardAsync(
        ScheduleBoardQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var range = ResolveRange(query);
        var data = await ReadDataAsync(cancellationToken);
        var workshops = data.Workshops.Select(ToWorkshop).Where(item => CanViewFaena(user, item.FaenaCodigo)).ToArray();
        var items = BuildItems(data, workshops)
            .Where(item => item.FechaInicio <= range.To && item.FechaFin >= range.From)
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.TallerCodigo) || Same(item.TallerCodigo, query.TallerCodigo))
            .Where(item => CanViewFaena(user, item.FaenaCodigo))
            .Where(item => query.IncludeClosed || item.Estado != ScheduleItemStatus.Completado)
            .OrderBy(item => item.FechaInicio)
            .ToArray();

        var loads = BuildLoads(items, workshops, range.From, range.To);
        var alerts = BuildAlerts(data.Alerts, items, loads, user, persistGenerated: false);
        var dependencies = data.Dependencies.Select(ToDependency).ToArray();
        var kanban = Enum.GetValues<ScheduleItemStatus>()
            .Select(status => new KanbanColumnResponse(status, items.Where(item => item.Estado == status).ToArray()))
            .ToArray();

        return new ScheduleBoardResponse(workshops, items, loads, kanban, dependencies, alerts);
    }

    public async Task<IReadOnlyCollection<WorkshopResponse>> ListWorkshopsAsync(
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.ReadOperationalRowsAsync(WorkshopsSchema, cancellationToken);
        return rows
            .Select(ToWorkshop)
            .Where(item => item.Activo)
            .Where(item => CanViewFaena(user, item.FaenaCodigo))
            .OrderBy(item => item.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<WorkshopResponse> UpsertWorkshopAsync(
        UpsertWorkshopRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlan(user);
        ValidateRequired(request.TallerCodigo, nameof(request.TallerCodigo));
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        ValidateRequired(request.FaenaCodigo, nameof(request.FaenaCodigo));
        ValidateRequired(request.Horario, nameof(request.Horario));
        ValidateRequired(request.Especialidad, nameof(request.Especialidad));
        if (request.CapacidadDiariaHH < 0 || request.CapacidadEquipos < 0)
        {
            throw new DomainException("La capacidad del taller no puede ser negativa.");
        }

        EnsureFaenaAccess(user, request.FaenaCodigo);
        var rows = (await _dbContext.ReadOperationalRowsAsync(WorkshopsSchema, cancellationToken)).ToList();
        var index = rows.FindIndex(row => Same(row.GetValue("TallerCodigo"), request.TallerCodigo));
        var previous = index >= 0 ? rows[index] : null;
        var updated = WorkshopRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["TallerCodigo"] = NormalizeCode(request.TallerCodigo),
            ["Nombre"] = NormalizeText(request.Nombre),
            ["FaenaCodigo"] = NormalizeCode(request.FaenaCodigo),
            ["CapacidadDiariaHH"] = FormatNumber(request.CapacidadDiariaHH),
            ["CapacidadEquipos"] = request.CapacidadEquipos.ToString(CultureInfo.InvariantCulture),
            ["Horario"] = NormalizeText(request.Horario),
            ["Especialidad"] = NormalizeText(request.Especialidad),
            ["Activo"] = request.Activo.ToString(CultureInfo.InvariantCulture)
        });

        if (index >= 0)
        {
            rows[index] = updated;
        }
        else
        {
            rows.Add(updated);
        }

        await _dbContext.SaveOperationalRowsAsync(WorkshopsSchema, rows, cancellationToken);
        await RecordAuditAsync(user, previous is null ? "scheduling.workshop_created" : "scheduling.workshop_updated", request.TallerCodigo, previous, updated, request.FaenaCodigo, request.Reason, cancellationToken);
        return ToWorkshop(updated);
    }

    public async Task<ScheduleWorkOrderResponse> ScheduleWorkOrderAsync(
        string numeroOt,
        ScheduleWorkOrderPlanningRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlan(user);
        ValidateRequired(numeroOt, nameof(numeroOt));
        ValidateRequired(request.TallerCodigo, nameof(request.TallerCodigo));
        ValidateRequired(request.Reason, nameof(request.Reason));
        if (request.FechaFin <= request.FechaInicio)
        {
            throw new DomainException("La fecha fin debe ser posterior a la fecha inicio.");
        }

        var data = await ReadDataAsync(cancellationToken);
        var workshop = data.Workshops.Select(ToWorkshop).FirstOrDefault(item => Same(item.TallerCodigo, request.TallerCodigo) && item.Activo)
            ?? throw new DomainException("El taller no existe o esta inactivo.");
        EnsureFaenaAccess(user, workshop.FaenaCodigo);

        var workOrder = data.WorkOrders.FirstOrDefault(row => Same(row.GetValue("NumeroOT"), numeroOt))
            ?? throw new DomainException("La OT no existe.");
        var faenaCodigo = FirstNonEmpty(workOrder.GetValue("FaenaCodigo"), workshop.FaenaCodigo) ?? workshop.FaenaCodigo;
        EnsureFaenaAccess(user, faenaCodigo);

        var scheduleRows = data.ScheduleItems.ToList();
        var existingIndex = scheduleRows.FindIndex(row => Same(row.GetValue("NumeroOT"), numeroOt));
        var previous = existingIndex >= 0 ? scheduleRows[existingIndex] : null;
        var row = ScheduleRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProgramacionId"] = previous?.GetValue("ProgramacionId") ?? NewId("PROG"),
            ["NumeroOT"] = NormalizeCode(numeroOt),
            ["TallerCodigo"] = NormalizeCode(request.TallerCodigo),
            ["FaenaCodigo"] = NormalizeCode(faenaCodigo),
            ["ActivoCodigo"] = NormalizeCode(workOrder.GetValue("ActivoCodigo")),
            ["TecnicoUserId"] = NormalizeText(request.TecnicoUserId),
            ["FechaInicio"] = FormatDate(request.FechaInicio),
            ["FechaFin"] = FormatDate(request.FechaFin),
            ["HHEstimadas"] = FormatNumber(request.HHEstimadas),
            ["Estado"] = ScheduleItemStatus.Programado.ToString(),
            ["Prioridad"] = NormalizeText(workOrder.GetValue("Prioridad")) ?? "Media",
            ["Criticidad"] = NormalizeText(workOrder.GetValue("Criticidad")) ?? "Media",
            ["Motivo"] = NormalizeText(request.Reason),
            ["ActualizadoPor"] = user.UserId,
            ["ActualizadoEnUtc"] = FormatDate(DateTimeOffset.UtcNow)
        });

        if (existingIndex >= 0)
        {
            scheduleRows[existingIndex] = row;
        }
        else
        {
            scheduleRows.Add(row);
        }

        var candidateData = data with { ScheduleItems = scheduleRows };
        var workshops = candidateData.Workshops.Select(ToWorkshop).ToArray();
        var candidateItems = BuildItems(candidateData, workshops);
        var range = new DateRange(request.FechaInicio.Date, request.FechaFin.Date);
        var loads = BuildLoads(candidateItems, workshops, range.From, range.To);
        var warnings = loads
            .Where(load => Same(load.TallerCodigo, request.TallerCodigo) && load.Sobrecargado)
            .Select(load => $"Taller {load.TallerNombre} sobrecargado el {load.Fecha:yyyy-MM-dd}: {load.HHProgramadas}/{load.CapacidadHH} HH.")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await _workOrderService.ScheduleAsync(numeroOt, new ScheduleWorkOrderRequest(request.FechaInicio, request.Reason, request.FechaFin), user, cancellationToken);
        await _dbContext.SaveOperationalRowsAsync(ScheduleSchema, scheduleRows, cancellationToken);

        var alerts = warnings.Length > 0
            ? await SaveGeneratedAlertsAsync(candidateData, candidateItems, loads, user, cancellationToken)
            : [];

        await RecordAuditAsync(user, previous is null ? "scheduling.work_order_scheduled" : "scheduling.work_order_rescheduled", numeroOt, previous, row, faenaCodigo, request.Reason, cancellationToken);
        var item = BuildItems(candidateData, workshops).First(item => Same(item.NumeroOT, numeroOt));
        return new ScheduleWorkOrderResponse(item, warnings, alerts);
    }

    public async Task<ScheduleDependencyResponse> AddDependencyAsync(
        AddScheduleDependencyRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanPlan(user);
        ValidateRequired(request.PredecessorNumeroOT, nameof(request.PredecessorNumeroOT));
        ValidateRequired(request.SuccessorNumeroOT, nameof(request.SuccessorNumeroOT));
        if (Same(request.PredecessorNumeroOT, request.SuccessorNumeroOT))
        {
            throw new DomainException("Una OT no puede depender de si misma.");
        }

        var rows = (await _dbContext.ReadOperationalRowsAsync(DependenciesSchema, cancellationToken)).ToList();
        if (rows.Any(row => Same(row.GetValue("PredecessorNumeroOT"), request.PredecessorNumeroOT) && Same(row.GetValue("SuccessorNumeroOT"), request.SuccessorNumeroOT)))
        {
            throw new DomainException("La dependencia ya existe.");
        }

        var rowToCreate = DependencyRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["DependenciaId"] = NewId("DEP"),
            ["PredecessorNumeroOT"] = NormalizeCode(request.PredecessorNumeroOT),
            ["SuccessorNumeroOT"] = NormalizeCode(request.SuccessorNumeroOT),
            ["Tipo"] = NormalizeText(request.Tipo) ?? "FinishToStart",
            ["Motivo"] = NormalizeText(request.Motivo)
        });

        rows.Add(rowToCreate);
        await _dbContext.SaveOperationalRowsAsync(DependenciesSchema, rows, cancellationToken);
        await RecordAuditAsync(user, "scheduling.dependency_added", rowToCreate.GetValue("DependenciaId") ?? string.Empty, null, rowToCreate, null, request.Motivo, cancellationToken);
        return new ScheduleDependencyResponse(
            rowToCreate.GetValue("DependenciaId") ?? string.Empty,
            rowToCreate.GetValue("PredecessorNumeroOT") ?? string.Empty,
            rowToCreate.GetValue("SuccessorNumeroOT") ?? string.Empty,
            rowToCreate.GetValue("Tipo") ?? "FinishToStart",
            EmptyToNull(rowToCreate.GetValue("Motivo")));
    }

    public async Task<IReadOnlyCollection<ScheduleAlertResponse>> ListAlertsAsync(
        ScheduleBoardQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var board = await GetBoardAsync(query, user, cancellationToken);
        return board.Alerts;
    }

    private async Task<IReadOnlyCollection<ScheduleAlertResponse>> SaveGeneratedAlertsAsync(
        SchedulingData data,
        IReadOnlyCollection<ScheduleItemResponse> items,
        IReadOnlyCollection<WorkshopLoadResponse> loads,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var alerts = BuildAlerts(data.Alerts, items, loads, user, persistGenerated: true);
        if (alerts.Count == 0)
        {
            return [];
        }

        var rows = data.Alerts.ToList();
        foreach (var alert in alerts)
        {
            var causeKey = AlertCauseKey(alert);
            var index = rows.FindIndex(row => Same(row.GetValue("CauseKey"), causeKey) && !ParseBool(row.GetValue("Resolved")));
            var row = AlertRow(alert, causeKey);
            if (index >= 0)
            {
                rows[index] = row;
            }
            else
            {
                rows.Add(row);
            }
        }

        await _dbContext.SaveOperationalRowsAsync(AlertsSchema, rows, cancellationToken);
        return alerts;
    }

    private async Task<SchedulingData> ReadDataAsync(CancellationToken cancellationToken)
    {
        return new SchedulingData(
            await _dbContext.ReadOperationalRowsAsync(WorkshopsSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(ScheduleSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(DependenciesSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(AlertsSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(WorkOrdersSchema, cancellationToken),
            await _dbContext.ReadOperationalRowsAsync(AssetsSchema, cancellationToken));
    }

    private static IReadOnlyCollection<ScheduleItemResponse> BuildItems(SchedulingData data, IReadOnlyCollection<WorkshopResponse> workshops)
    {
        return data.ScheduleItems
            .Select(row =>
            {
                var numeroOt = row.GetValue("NumeroOT") ?? string.Empty;
                var order = data.WorkOrders.FirstOrDefault(item => Same(item.GetValue("NumeroOT"), numeroOt));
                var assetCode = FirstNonEmpty(row.GetValue("ActivoCodigo"), order?.GetValue("ActivoCodigo")) ?? string.Empty;
                var asset = data.Assets.FirstOrDefault(item => Same(item.GetValue("Codigo"), assetCode));
                var workshop = workshops.FirstOrDefault(item => Same(item.TallerCodigo, row.GetValue("TallerCodigo")));
                var start = ParseDate(row.GetValue("FechaInicio")) ?? ParseDate(order?.GetValue("FechaInicioProgramada")) ?? DateTimeOffset.UtcNow;
                var end = ParseDate(row.GetValue("FechaFin")) ?? ParseDate(order?.GetValue("FechaFinProgramada")) ?? start.AddHours(2);
                var status = DeriveStatus(row, order, end);
                return new ScheduleItemResponse(
                    row.GetValue("ProgramacionId") ?? string.Empty,
                    numeroOt,
                    row.GetValue("TallerCodigo") ?? string.Empty,
                    workshop?.Nombre ?? row.GetValue("TallerCodigo") ?? string.Empty,
                    FirstNonEmpty(row.GetValue("FaenaCodigo"), order?.GetValue("FaenaCodigo"), asset?.GetValue("FaenaCodigo")) ?? string.Empty,
                    assetCode,
                    EmptyToNull(asset?.GetValue("Nombre")),
                    EmptyToNull(row.GetValue("TecnicoUserId")),
                    start,
                    end,
                    ParseDecimal(row.GetValue("HHEstimadas")),
                    status,
                    FirstNonEmpty(row.GetValue("Prioridad"), order?.GetValue("Prioridad")) ?? "Media",
                    FirstNonEmpty(row.GetValue("Criticidad"), order?.GetValue("Criticidad")) ?? "Media",
                    FirstNonEmpty(order?.GetValue("Descripcion"), row.GetValue("Motivo")) ?? string.Empty);
            })
            .ToArray();
    }

    private static IReadOnlyCollection<WorkshopLoadResponse> BuildLoads(
        IReadOnlyCollection<ScheduleItemResponse> items,
        IReadOnlyCollection<WorkshopResponse> workshops,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var loads = new List<WorkshopLoadResponse>();
        var fromDate = DateOnly.FromDateTime(from.Date);
        var toDate = DateOnly.FromDateTime(to.Date);
        foreach (var workshop in workshops)
        {
            for (var date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                var dayItems = items.Where(item => Same(item.TallerCodigo, workshop.TallerCodigo) && IntersectsDate(item, date)).ToArray();
                var hh = dayItems.Sum(item => SpreadHoursOnDate(item, date));
                var equipos = dayItems.Select(item => item.ActivoCodigo).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                loads.Add(new WorkshopLoadResponse(
                    workshop.TallerCodigo,
                    workshop.Nombre,
                    date,
                    workshop.CapacidadDiariaHH,
                    Math.Round(hh, 2),
                    workshop.CapacidadEquipos,
                    equipos,
                    hh > workshop.CapacidadDiariaHH || equipos > workshop.CapacidadEquipos));
            }
        }

        return loads;
    }

    private IReadOnlyCollection<ScheduleAlertResponse> BuildAlerts(
        IReadOnlyCollection<DataRow> persistedAlerts,
        IReadOnlyCollection<ScheduleItemResponse> items,
        IReadOnlyCollection<WorkshopLoadResponse> loads,
        UserAccessContext user,
        bool persistGenerated)
    {
        var alerts = new List<ScheduleAlertResponse>();
        alerts.AddRange(persistedAlerts.Select(ToAlert).Where(alert => !alert.Resolved && CanViewFaena(user, alert.FaenaCodigo)));

        foreach (var load in loads.Where(item => item.Sobrecargado))
        {
            alerts.Add(new ScheduleAlertResponse(
                NewId("PAL"),
                load.HHProgramadas > load.CapacidadHH ? ScheduleAlertType.TallerSobrecargado : ScheduleAlertType.ProgramacionExcedeCapacidad,
                "Warning",
                $"Taller {load.TallerNombre} excede capacidad el {load.Fecha:yyyy-MM-dd}: {load.HHProgramadas}/{load.CapacidadHH} HH, {load.EquiposProgramados}/{load.CapacidadEquipos} equipos.",
                load.TallerCodigo,
                null,
                null,
                DateTimeOffset.UtcNow,
                false));
        }

        foreach (var item in items.Where(item => item.Estado == ScheduleItemStatus.Atrasado))
        {
            alerts.Add(new ScheduleAlertResponse(
                NewId("PAL"),
                item.Criticidad.Equals("Critica", StringComparison.OrdinalIgnoreCase) ? ScheduleAlertType.TrabajoCriticoAtrasado : ScheduleAlertType.OTVencida,
                item.Criticidad.Equals("Critica", StringComparison.OrdinalIgnoreCase) ? "Critical" : "Warning",
                $"OT {item.NumeroOT} atrasada desde {item.FechaFin:yyyy-MM-dd}.",
                item.TallerCodigo,
                item.NumeroOT,
                item.FaenaCodigo,
                DateTimeOffset.UtcNow,
                false));
        }

        return persistGenerated ? alerts.Where(alert => alert.TallerCodigo is not null || alert.NumeroOT is not null).ToArray() : alerts.DistinctBy(AlertCauseKey).ToArray();
    }

    private static WorkshopResponse ToWorkshop(DataRow row)
    {
        return new WorkshopResponse(
            row.GetValue("TallerCodigo") ?? string.Empty,
            row.GetValue("Nombre") ?? string.Empty,
            row.GetValue("FaenaCodigo") ?? string.Empty,
            ParseDecimal(row.GetValue("CapacidadDiariaHH")),
            ParseInt(row.GetValue("CapacidadEquipos")),
            row.GetValue("Horario") ?? string.Empty,
            row.GetValue("Especialidad") ?? string.Empty,
            ParseBool(row.GetValue("Activo"), true));
    }

    private static GanttDependencyResponse ToDependency(DataRow row)
    {
        return new GanttDependencyResponse(
            row.GetValue("DependenciaId") ?? string.Empty,
            row.GetValue("PredecessorNumeroOT") ?? string.Empty,
            row.GetValue("SuccessorNumeroOT") ?? string.Empty,
            row.GetValue("Tipo") ?? "FinishToStart",
            EmptyToNull(row.GetValue("Motivo")));
    }

    private static ScheduleAlertResponse ToAlert(DataRow row)
    {
        return new ScheduleAlertResponse(
            row.GetValue("AlertId") ?? string.Empty,
            ParseEnum(row.GetValue("Tipo"), ScheduleAlertType.ProgramacionExcedeCapacidad),
            row.GetValue("Severity") ?? "Warning",
            row.GetValue("Message") ?? string.Empty,
            EmptyToNull(row.GetValue("TallerCodigo")),
            EmptyToNull(row.GetValue("NumeroOT")),
            EmptyToNull(row.GetValue("FaenaCodigo")),
            ParseDate(row.GetValue("CreatedAtUtc")) ?? DateTimeOffset.MinValue,
            ParseBool(row.GetValue("Resolved")));
    }

    private static DataRow WorkshopRow(IReadOnlyDictionary<string, string?> values) => Row(WorkshopColumns, values);
    private static DataRow ScheduleRow(IReadOnlyDictionary<string, string?> values) => Row(ScheduleColumns, values);
    private static DataRow DependencyRow(IReadOnlyDictionary<string, string?> values) => Row(DependencyColumns, values);
    private static DataRow AlertRow(ScheduleAlertResponse alert, string causeKey) => Row(AlertColumns, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["AlertId"] = alert.AlertId,
        ["Tipo"] = alert.Tipo.ToString(),
        ["Severity"] = alert.Severity,
        ["Message"] = alert.Message,
        ["TallerCodigo"] = alert.TallerCodigo,
        ["NumeroOT"] = alert.NumeroOT,
        ["FaenaCodigo"] = alert.FaenaCodigo,
        ["CauseKey"] = causeKey,
        ["CreatedAtUtc"] = FormatDate(alert.CreatedAtUtc),
        ["Resolved"] = alert.Resolved.ToString(CultureInfo.InvariantCulture)
    });

    private static DataRow Row(IEnumerable<string> columns, IReadOnlyDictionary<string, string?> values)
    {
        return new DataRow(columns.ToDictionary(column => column, column => values.TryGetValue(column, out var value) ? value : null, StringComparer.OrdinalIgnoreCase));
    }

    private async Task RecordAuditAsync(
        UserAccessContext user,
        string action,
        string entityId,
        DataRow? previous,
        DataRow updated,
        string? faenaCodigo,
        string? reason,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            AuditModules.WorkOrders,
            "Scheduling",
            entityId,
            previous is null ? null : Serialize(previous),
            Serialize(updated),
            faenaCodigo,
            AuditSeverity.Medium,
            reason),
            cancellationToken);
    }

    private static DateRange ResolveRange(ScheduleBoardQuery query)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var from = query.From?.Date ?? query.View switch
        {
            ScheduleViewMode.Diario => today,
            ScheduleViewMode.Mensual => new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero),
            _ => today.AddDays(-(int)today.DayOfWeek)
        };
        var to = query.To?.Date ?? query.View switch
        {
            ScheduleViewMode.Diario => from.AddDays(1).AddTicks(-1),
            ScheduleViewMode.Mensual => from.AddMonths(1).AddTicks(-1),
            _ => from.AddDays(7).AddTicks(-1)
        };
        return new DateRange(from, to);
    }

    private static ScheduleItemStatus DeriveStatus(DataRow scheduleRow, DataRow? order, DateTimeOffset end)
    {
        var orderStatus = order?.GetValue("Estado") ?? string.Empty;
        if (orderStatus is "CerradaTecnicamente" or "ValidadaPlanificacion")
        {
            return ScheduleItemStatus.Completado;
        }

        if (orderStatus is "EnEjecucion" or "Pausada")
        {
            return ScheduleItemStatus.EnProceso;
        }

        if (DateTimeOffset.UtcNow > end && orderStatus is not "Anulada")
        {
            return ScheduleItemStatus.Atrasado;
        }

        return ParseEnum(scheduleRow.GetValue("Estado"), ScheduleItemStatus.Programado);
    }

    private static bool IntersectsDate(ScheduleItemResponse item, DateOnly date)
    {
        var start = DateOnly.FromDateTime(item.FechaInicio.Date);
        var end = DateOnly.FromDateTime(item.FechaFin.Date);
        return date >= start && date <= end;
    }

    private static decimal SpreadHoursOnDate(ScheduleItemResponse item, DateOnly date)
    {
        if (!IntersectsDate(item, date))
        {
            return 0;
        }

        var start = DateOnly.FromDateTime(item.FechaInicio.Date);
        var end = DateOnly.FromDateTime(item.FechaFin.Date);
        var days = Math.Max(1, end.DayNumber - start.DayNumber + 1);
        return item.HHEstimadas / days;
    }

    private static string AlertCauseKey(ScheduleAlertResponse alert)
    {
        return $"{alert.Tipo}:{alert.TallerCodigo}:{alert.NumeroOT}:{alert.Message}".ToUpperInvariant();
    }

    private void EnsureCanPlan(UserAccessContext user)
    {
        if (!user.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) &&
            !user.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase) &&
            !user.Roles.Contains(AuthRoles.MaintenanceSupervisor, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("El usuario no puede gestionar programacion.");
        }
    }

    private void EnsureFaenaAccess(UserAccessContext user, string? faenaCodigo)
    {
        if (user.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) ||
            user.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase) ||
            user.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(faenaCodigo) ||
            user.Faenas.Count == 0)
        {
            return;
        }

        if (!user.Faenas.Contains(faenaCodigo, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena indicada.");
        }
    }

    private bool CanViewFaena(UserAccessContext user, string? faenaCodigo)
    {
        return user.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) ||
               user.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase) ||
               user.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase) ||
               string.IsNullOrWhiteSpace(faenaCodigo) ||
               user.Faenas.Count == 0 ||
               user.Faenas.Contains(faenaCodigo, StringComparer.OrdinalIgnoreCase);
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
    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    private static string FormatDate(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    private static string FormatNumber(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static DateTimeOffset? ParseDate(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    private static decimal ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    private static int ParseInt(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    private static bool ParseBool(string? value, bool fallback = false) => bool.TryParse(value, out var parsed) ? parsed : fallback;
    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct => Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, prefix.Length + 33)].ToUpperInvariant();
    private static string Serialize(DataRow row) => JsonSerializer.Serialize(row.Values);

    private sealed record DateRange(DateTimeOffset From, DateTimeOffset To);

    private sealed record SchedulingData(
        IReadOnlyList<DataRow> Workshops,
        IReadOnlyList<DataRow> ScheduleItems,
        IReadOnlyList<DataRow> Dependencies,
        IReadOnlyList<DataRow> Alerts,
        IReadOnlyList<DataRow> WorkOrders,
        IReadOnlyList<DataRow> Assets);

    private static readonly string[] WorkshopColumns =
    [
        "TallerCodigo",
        "Nombre",
        "FaenaCodigo",
        "CapacidadDiariaHH",
        "CapacidadEquipos",
        "Horario",
        "Especialidad",
        "Activo"
    ];

    private static readonly string[] ScheduleColumns =
    [
        "ProgramacionId",
        "NumeroOT",
        "TallerCodigo",
        "FaenaCodigo",
        "ActivoCodigo",
        "TecnicoUserId",
        "FechaInicio",
        "FechaFin",
        "HHEstimadas",
        "Estado",
        "Prioridad",
        "Criticidad",
        "Motivo",
        "ActualizadoPor",
        "ActualizadoEnUtc"
    ];

    private static readonly string[] DependencyColumns =
    [
        "DependenciaId",
        "PredecessorNumeroOT",
        "SuccessorNumeroOT",
        "Tipo",
        "Motivo"
    ];

    private static readonly string[] AlertColumns =
    [
        "AlertId",
        "Tipo",
        "Severity",
        "Message",
        "TallerCodigo",
        "NumeroOT",
        "FaenaCodigo",
        "CauseKey",
        "CreatedAtUtc",
        "Resolved"
    ];
}
