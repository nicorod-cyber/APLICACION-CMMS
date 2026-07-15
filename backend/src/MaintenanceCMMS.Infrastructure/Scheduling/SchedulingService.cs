using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Scheduling;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Scheduling;

/// <summary>Relational scheduling service. Dependencies are foreign-key links, never embedded JSON.</summary>
public sealed class SchedulingService : ISchedulingService
{
    private readonly CmmsDbContext _db;
    public SchedulingService(CmmsDbContext dbContext) => _db = dbContext;

    public async Task<IReadOnlyCollection<WorkshopResponse>> ListWorkshopsAsync(UserAccessContext user, CancellationToken ct)
    {
        EnsureView(user);
        return (await _db.Workshops.AsNoTracking().Include(x => x.Faena).Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync(ct)).Where(x => CanAccess(user, x.Faena.Code)).Select(ToWorkshop).ToArray();
    }

    public async Task<WorkshopResponse> UpsertWorkshopAsync(UpsertWorkshopRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureManage(user); Required(request.TallerCodigo, nameof(request.TallerCodigo)); Required(request.Nombre, nameof(request.Nombre)); Required(request.FaenaCodigo, nameof(request.FaenaCodigo)); Required(request.Horario, nameof(request.Horario)); Required(request.Especialidad, nameof(request.Especialidad));
        if (request.CapacidadDiariaHH < 0 || request.CapacidadEquipos < 0) throw new DomainException("Las capacidades del taller no pueden ser negativas.");
        var faena = await _db.Faenas.SingleOrDefaultAsync(x => x.Code == Code(request.FaenaCodigo), ct) ?? throw new DomainException($"La faena '{request.FaenaCodigo}' no existe."); EnsureAccess(user, faena.Code);
        var workshop = await _db.Workshops.Include(x => x.Faena).SingleOrDefaultAsync(x => x.Code == Code(request.TallerCodigo), ct);
        if (workshop is null) { workshop = new WorkshopEntity { Code = Code(request.TallerCodigo)!, FaenaId = faena.Id, CreatedByUserId = user.UserId }; _db.Workshops.Add(workshop); }
        workshop.Name = request.Nombre.Trim(); workshop.FaenaId = faena.Id; workshop.Faena = faena; workshop.DailyLaborCapacity = request.CapacidadDiariaHH; workshop.EquipmentCapacity = request.CapacidadEquipos; workshop.Schedule = request.Horario.Trim(); workshop.Specialty = request.Especialidad.Trim(); workshop.IsActive = request.Activo; workshop.UpdatedByUserId = user.UserId; workshop.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct); return ToWorkshop(workshop);
    }

    public async Task<ScheduleWorkOrderResponse> ScheduleWorkOrderAsync(string numeroOt, ScheduleWorkOrderPlanningRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureManage(user); Required(numeroOt, nameof(numeroOt)); Required(request.TallerCodigo, nameof(request.TallerCodigo)); Required(request.Reason, nameof(request.Reason)); if (request.FechaFin <= request.FechaInicio) throw new DomainException("La fecha de termino debe ser posterior al inicio."); if (request.HHEstimadas <= 0) throw new DomainException("Las HH estimadas deben ser positivas.");
        var order = await _db.WorkOrders.Include(x => x.Asset).Include(x => x.OperationalUnit).Include(x => x.Faena).Include(x => x.Priority).Include(x => x.Criticality).SingleOrDefaultAsync(x => x.WorkOrderNumber == Code(numeroOt), ct) ?? throw new DomainException($"La OT '{numeroOt}' no existe."); EnsureAccess(user, order.Faena.Code);
        var workshop = await _db.Workshops.Include(x => x.Faena).SingleOrDefaultAsync(x => x.Code == Code(request.TallerCodigo) && x.IsActive, ct) ?? throw new DomainException($"El taller '{request.TallerCodigo}' no existe o esta inactivo."); if (workshop.FaenaId != order.FaenaId) throw new DomainException("La OT y el taller deben pertenecer a la misma faena.");
        var schedule = await _db.WorkOrderSchedules.Include(x => x.Workshop).SingleOrDefaultAsync(x => x.WorkOrderId == order.Id, ct);
        var warnings = new List<string>();
        var load = await DailyLoadAsync(workshop, request.FechaInicio.Date, ct);
        if (!request.OverrideCapacity && (load.Hours + request.HHEstimadas > workshop.DailyLaborCapacity || load.Count + 1 > workshop.EquipmentCapacity)) throw new DomainException("La programacion excede la capacidad del taller. Use OverrideCapacity con autorizacion explicita.");
        if (request.OverrideCapacity && (load.Hours + request.HHEstimadas > workshop.DailyLaborCapacity || load.Count + 1 > workshop.EquipmentCapacity)) warnings.Add("La programacion excede la capacidad del taller.");
        if (schedule is null) { schedule = new WorkOrderScheduleEntity { WorkOrderId = order.Id, WorkshopId = workshop.Id, CreatedByUserId = user.UserId }; _db.WorkOrderSchedules.Add(schedule); }
        schedule.Workshop = workshop; schedule.StartsAtUtc = request.FechaInicio; schedule.EndsAtUtc = request.FechaFin; schedule.EstimatedLaborHours = request.HHEstimadas; schedule.TechnicianUserId = Text(request.TecnicoUserId); schedule.Status = (int)ScheduleItemStatus.Programado; schedule.UpdatedAtUtc = DateTimeOffset.UtcNow; schedule.UpdatedByUserId = user.UserId;
        var alerts = new List<ScheduleAlertResponse>();
        if (warnings.Count > 0) { var alert = new ScheduleAlertEntity { Type = (int)ScheduleAlertType.ProgramacionExcedeCapacidad, Severity = "High", Message = warnings[0], WorkshopId = workshop.Id, WorkOrderId = order.Id, FaenaId = order.FaenaId, RaisedAtUtc = DateTimeOffset.UtcNow }; _db.ScheduleAlerts.Add(alert); alerts.Add(ToAlert(alert, workshop, order)); }
        await _db.SaveChangesAsync(ct); return new(ToItem(schedule, order, workshop), warnings, alerts);
    }

    public async Task<ScheduleDependencyResponse> AddDependencyAsync(AddScheduleDependencyRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureManage(user); Required(request.PredecessorNumeroOT, nameof(request.PredecessorNumeroOT)); Required(request.SuccessorNumeroOT, nameof(request.SuccessorNumeroOT)); if (Code(request.PredecessorNumeroOT) == Code(request.SuccessorNumeroOT)) throw new DomainException("Una OT no puede depender de si misma.");
        var schedules = await _db.WorkOrderSchedules.Include(x => x.WorkOrder).Include(x => x.Workshop).ThenInclude(x => x.Faena).Where(x => x.WorkOrder.WorkOrderNumber == Code(request.PredecessorNumeroOT) || x.WorkOrder.WorkOrderNumber == Code(request.SuccessorNumeroOT)).ToListAsync(ct);
        var predecessor = schedules.SingleOrDefault(x => x.WorkOrder.WorkOrderNumber == Code(request.PredecessorNumeroOT)) ?? throw new DomainException("La OT predecesora no esta programada."); var successor = schedules.SingleOrDefault(x => x.WorkOrder.WorkOrderNumber == Code(request.SuccessorNumeroOT)) ?? throw new DomainException("La OT sucesora no esta programada."); EnsureAccess(user, predecessor.Workshop.Faena.Code); EnsureAccess(user, successor.Workshop.Faena.Code);
        if (await HasPathAsync(successor.Id, predecessor.Id, ct)) throw new DomainException("La dependencia generaria un ciclo.");
        if (await _db.ScheduleDependencies.AnyAsync(x => x.PredecessorScheduleId == predecessor.Id && x.SuccessorScheduleId == successor.Id, ct)) throw new DomainException("La dependencia ya existe.");
        var entity = new ScheduleDependencyEntity { PredecessorScheduleId = predecessor.Id, SuccessorScheduleId = successor.Id, Type = Text(request.Tipo) ?? "FinishToStart", Reason = Text(request.Motivo), CreatedByUserId = user.UserId }; _db.ScheduleDependencies.Add(entity); await _db.SaveChangesAsync(ct); return new(entity.Id.ToString("N"), predecessor.WorkOrder.WorkOrderNumber, successor.WorkOrder.WorkOrderNumber, entity.Type, entity.Reason);
    }

    public async Task<ScheduleBoardResponse> GetBoardAsync(ScheduleBoardQuery query, UserAccessContext user, CancellationToken ct)
    {
        EnsureView(user); var from = query.From ?? DateTimeOffset.UtcNow.AddDays(-7); var to = query.To ?? DateTimeOffset.UtcNow.AddDays(30); if (to < from) throw new DomainException("El rango de fechas no es valido.");
        var workshops = _db.Workshops.AsNoTracking().Include(x => x.Faena).Where(x => x.IsActive).AsQueryable(); if (!string.IsNullOrWhiteSpace(query.FaenaCodigo)) workshops = workshops.Where(x => x.Faena.Code == Code(query.FaenaCodigo)); if (!string.IsNullOrWhiteSpace(query.TallerCodigo)) workshops = workshops.Where(x => x.Code == Code(query.TallerCodigo));
        var workshopList = (await workshops.ToListAsync(ct)).Where(x => CanAccess(user, x.Faena.Code)).ToArray(); var ids = workshopList.Select(x => x.Id).ToArray();
        var schedules = await _db.WorkOrderSchedules.AsNoTracking().Include(x => x.Workshop).ThenInclude(x => x.Faena).Include(x => x.WorkOrder).ThenInclude(x => x.Asset).Include(x => x.WorkOrder).ThenInclude(x => x.OperationalUnit).Include(x => x.WorkOrder).ThenInclude(x => x.Priority).Include(x => x.WorkOrder).ThenInclude(x => x.Criticality).Where(x => ids.Contains(x.WorkshopId) && x.StartsAtUtc <= to && x.EndsAtUtc >= from).ToListAsync(ct);
        if (!query.IncludeClosed) schedules = schedules.Where(x => x.Status != (int)ScheduleItemStatus.Completado).ToList(); var items = schedules.Select(x => ToItem(x, x.WorkOrder, x.Workshop)).ToArray();
        var dependencies = await _db.ScheduleDependencies.AsNoTracking().Include(x => x.PredecessorSchedule).ThenInclude(x => x.WorkOrder).Include(x => x.SuccessorSchedule).ThenInclude(x => x.WorkOrder).Where(x => ids.Contains(x.PredecessorSchedule.WorkshopId) || ids.Contains(x.SuccessorSchedule.WorkshopId)).ToListAsync(ct);
        var alerts = await AlertsAsync(ids, from, to, ct); var loads = await LoadAsync(workshopList, schedules, from, to, ct); return new(workshopList.Select(ToWorkshop).ToArray(), items, loads, Enum.GetValues<ScheduleItemStatus>().Select(s => new KanbanColumnResponse(s, items.Where(x => x.Estado == s).ToArray())).ToArray(), dependencies.Select(x => new GanttDependencyResponse(x.Id.ToString("N"), x.PredecessorSchedule.WorkOrder.WorkOrderNumber, x.SuccessorSchedule.WorkOrder.WorkOrderNumber, x.Type, x.Reason)).ToArray(), alerts);
    }

    public async Task<IReadOnlyCollection<ScheduleAlertResponse>> ListAlertsAsync(ScheduleBoardQuery query, UserAccessContext user, CancellationToken ct)
    {
        EnsureView(user); var workshops = await _db.Workshops.AsNoTracking().Include(x => x.Faena).Where(x => string.IsNullOrWhiteSpace(query.FaenaCodigo) || x.Faena.Code == Code(query.FaenaCodigo)).ToListAsync(ct); var ids = workshops.Where(x => CanAccess(user, x.Faena.Code)).Select(x => x.Id).ToArray(); return await AlertsAsync(ids, query.From ?? DateTimeOffset.MinValue, query.To ?? DateTimeOffset.MaxValue, ct);
    }

    private async Task<IReadOnlyCollection<ScheduleAlertResponse>> AlertsAsync(Guid[] workshopIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) => (await _db.ScheduleAlerts.AsNoTracking().Include(x => x.Workshop).Include(x => x.WorkOrder).Include(x => x.Faena).Where(x => (x.WorkshopId == null || workshopIds.Contains(x.WorkshopId.Value)) && x.RaisedAtUtc >= from && x.RaisedAtUtc <= to).OrderByDescending(x => x.RaisedAtUtc).ToListAsync(ct)).Select(x => ToAlert(x, x.Workshop, x.WorkOrder)).ToArray();
    private async Task<IReadOnlyCollection<WorkshopLoadResponse>> LoadAsync(IEnumerable<WorkshopEntity> workshops, IEnumerable<WorkOrderScheduleEntity> schedules, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) { var result = new List<WorkshopLoadResponse>(); foreach (var w in workshops) for (var d = DateOnly.FromDateTime(from.UtcDateTime.Date); d <= DateOnly.FromDateTime(to.UtcDateTime.Date); d = d.AddDays(1)) { var day = schedules.Where(x => x.WorkshopId == w.Id && x.StartsAtUtc.Date <= d.ToDateTime(TimeOnly.MinValue) && x.EndsAtUtc.Date >= d.ToDateTime(TimeOnly.MinValue)).ToArray(); var h = day.Sum(x => x.EstimatedLaborHours); result.Add(new(w.Code, w.Name, d, w.DailyLaborCapacity, h, w.EquipmentCapacity, day.Length, h > w.DailyLaborCapacity || day.Length > w.EquipmentCapacity)); } return result; }
    private async Task<(decimal Hours,int Count)> DailyLoadAsync(WorkshopEntity workshop, DateTimeOffset day, CancellationToken ct) { var start = new DateTimeOffset(day.UtcDateTime.Date, TimeSpan.Zero); var next = start.AddDays(1); var schedules = await _db.WorkOrderSchedules.AsNoTracking().Where(x => x.WorkshopId == workshop.Id && x.StartsAtUtc < next && x.EndsAtUtc >= start).ToListAsync(ct); return (schedules.Sum(x => x.EstimatedLaborHours), schedules.Count); }
    private async Task<bool> HasPathAsync(Guid from, Guid target, CancellationToken ct) { var queue = new Queue<Guid>(); var visited = new HashSet<Guid>(); queue.Enqueue(from); while (queue.Count > 0) { var current = queue.Dequeue(); if (!visited.Add(current)) continue; if (current == target) return true; foreach (var next in await _db.ScheduleDependencies.Where(x => x.PredecessorScheduleId == current).Select(x => x.SuccessorScheduleId).ToListAsync(ct)) queue.Enqueue(next); } return false; }
    private static WorkshopResponse ToWorkshop(WorkshopEntity x) => new(x.Code, x.Name, x.Faena.Code, x.DailyLaborCapacity, x.EquipmentCapacity, x.Schedule, x.Specialty, x.IsActive);
    private static ScheduleItemResponse ToItem(WorkOrderScheduleEntity x, WorkOrderEntity o, WorkshopEntity w) => new(x.Id.ToString("N"), o.WorkOrderNumber, w.Code, w.Name, w.Faena.Code, o.Asset?.Code ?? o.OperationalUnit?.Code ?? string.Empty, o.Asset?.Name ?? o.OperationalUnit?.Name, x.TechnicianUserId, x.StartsAtUtc, x.EndsAtUtc, x.EstimatedLaborHours, (ScheduleItemStatus)x.Status, o.Priority?.Code ?? string.Empty, o.Criticality?.Code ?? string.Empty, o.Description);
    private static ScheduleAlertResponse ToAlert(ScheduleAlertEntity x, WorkshopEntity? w, WorkOrderEntity? o) => new(x.Id.ToString("N"), (ScheduleAlertType)x.Type, x.Severity, x.Message, w?.Code, o?.WorkOrderNumber, x.Faena?.Code ?? w?.Faena?.Code, x.RaisedAtUtc, x.IsResolved);
    private static string? Text(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim(); private static string? Code(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim().ToUpperInvariant(); private static void Required(string? x, string n) { if (string.IsNullOrWhiteSpace(x)) throw new DomainException($"El campo {n} es obligatorio."); }
    private static bool CanAccess(UserAccessContext u, string code) => u.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase) || u.Faenas.Contains(code, StringComparer.OrdinalIgnoreCase); private static void EnsureAccess(UserAccessContext u, string code) { if (!CanAccess(u, code)) throw new UnauthorizedAccessException("No tiene acceso a la faena."); } private static void EnsureView(UserAccessContext u) { if (!(u.Roles.Contains(AuthRoles.Admin,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Planner,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.MaintenanceSupervisor,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.FaenaViewer,StringComparer.OrdinalIgnoreCase))) throw new UnauthorizedAccessException("No tiene permisos para ver programacion."); } private static void EnsureManage(UserAccessContext u) { if (!(u.Roles.Contains(AuthRoles.Admin,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Planner,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.MaintenanceSupervisor,StringComparer.OrdinalIgnoreCase))) throw new UnauthorizedAccessException("No tiene permisos para gestionar programacion."); }
}
