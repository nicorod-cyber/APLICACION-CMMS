using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Scheduling;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Scheduling;

/// <summary>Schedules work at global workshops; territory always comes from the work order.</summary>
public sealed class SchedulingService : ISchedulingService
{
    private readonly CmmsDbContext _db;
    public SchedulingService(CmmsDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<WorkshopResponse>> ListWorkshopsAsync(UserAccessContext user, CancellationToken ct)
    {
        EnsureView(user);
        var workshops = await _db.Workshops.AsNoTracking().Include(x => x.SupervisorUser).Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync(ct);
        return await MapWorkshopsAsync(workshops, ct);
    }

    public async Task<IReadOnlyCollection<WorkshopSupervisorResponse>> ListWorkshopSupervisorsAsync(UserAccessContext user, CancellationToken ct)
    {
        EnsureManage(user);
        return await _db.Users.AsNoTracking()
            .Where(x => x.IsActive && !x.IsLocked && x.Roles.Any(role => role.IsActive && role.Role.IsActive && (role.Role.Code == AuthRoles.MaintenanceSupervisor || role.Role.Code == AuthRoles.Admin)))
            .OrderBy(x => x.DisplayName).ThenBy(x => x.Username)
            .Select(x => new WorkshopSupervisorResponse(x.Id.ToString("D"), x.DisplayName, x.Username))
            .ToArrayAsync(ct);
    }
    public async Task<WorkshopResponse> UpsertWorkshopAsync(UpsertWorkshopRequest r, UserAccessContext u, CancellationToken ct)
    {
        EnsureManage(u); Required(r.TallerCodigo, nameof(r.TallerCodigo)); Required(r.Nombre, nameof(r.Nombre));
        if (r.CapacidadEquipos < 0) throw new DomainException("La capacidad de equipos no puede ser negativa.");
        var commune = Text(r.Comuna);
        var supervisor = await SupervisorAsync(r.SupervisorUsuarioId, ct);
        if (r.Activo && (commune is null || supervisor is null)) throw new DomainException("Un taller activo requiere comuna y responsable de taller vigente.");
        var workshop = await _db.Workshops.Include(x => x.SupervisorUser).SingleOrDefaultAsync(x => x.Code == Code(r.TallerCodigo), ct);
        if (workshop is null) { workshop = new WorkshopEntity { Code = Code(r.TallerCodigo)!, CreatedByUserId = u.UserId }; _db.Workshops.Add(workshop); }
        workshop.Name = r.Nombre.Trim(); workshop.EquipmentCapacity = r.CapacidadEquipos; workshop.Commune = commune; workshop.SupervisorUserId = supervisor?.Id; workshop.SupervisorUser = supervisor; workshop.IsActive = r.Activo; workshop.UpdatedByUserId = u.UserId; workshop.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct); return (await MapWorkshopsAsync([workshop], ct)).Single();
    }

    public async Task<ScheduleWorkOrderResponse> ScheduleWorkOrderAsync(string numeroOt, ScheduleWorkOrderPlanningRequest r, UserAccessContext u, CancellationToken ct)
    {
        EnsureManage(u); Required(numeroOt, nameof(numeroOt)); Required(r.TallerCodigo, nameof(r.TallerCodigo)); Required(r.Reason, nameof(r.Reason));
        if (r.FechaFin <= r.FechaInicio || r.HHEstimadas <= 0) throw new DomainException("Las fechas y HH estimadas de la programacion no son validas.");
        var order = await OrderAsync(numeroOt, ct); EnsureAccess(u, order.Faena.Code);
        var workshop = await _db.Workshops.Include(x => x.SupervisorUser).SingleOrDefaultAsync(x => x.Code == Code(r.TallerCodigo) && x.IsActive, ct) ?? throw new DomainException("El taller no existe o esta inactivo.");
        if (string.IsNullOrWhiteSpace(workshop.Commune) || workshop.SupervisorUser is null) throw new DomainException("El taller requiere regularizar comuna y supervisor antes de usarse.");
        var schedule = await _db.WorkOrderSchedules.SingleOrDefaultAsync(x => x.WorkOrderId == order.Id, ct);
        var load = await DailyLoadAsync(workshop.Id, r.FechaInicio, ct); var overload = load + 1 > workshop.EquipmentCapacity;
        if (overload && !r.OverrideCapacity) throw new DomainException("La programacion excede la capacidad de equipos del taller. Use OverrideCapacity con autorizacion explicita.");
        if (schedule is null) { schedule = new WorkOrderScheduleEntity { WorkOrderId = order.Id, WorkshopId = workshop.Id, CreatedByUserId = u.UserId }; _db.WorkOrderSchedules.Add(schedule); }
        schedule.WorkshopId = workshop.Id; schedule.Workshop = workshop; schedule.StartsAtUtc = r.FechaInicio; schedule.EndsAtUtc = r.FechaFin; schedule.EstimatedLaborHours = r.HHEstimadas; schedule.TechnicianUserId = Text(r.TecnicoUserId); schedule.Status = (int)ScheduleItemStatus.Programado; schedule.UpdatedByUserId = u.UserId; schedule.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var alerts = new List<ScheduleAlertResponse>();
        if (overload) { var alert = new ScheduleAlertEntity { Type = (int)ScheduleAlertType.ProgramacionExcedeCapacidad, Severity = "High", Message = "La programacion excede la capacidad de equipos del taller.", WorkshopId = workshop.Id, WorkOrderId = order.Id, FaenaId = order.FaenaId, RaisedAtUtc = DateTimeOffset.UtcNow }; _db.ScheduleAlerts.Add(alert); alerts.Add(ToAlert(alert, workshop, order)); }
        await _db.SaveChangesAsync(ct); return new(ToItem(schedule, order, workshop), overload ? ["La programacion excede la capacidad de equipos del taller."] : [], alerts);
    }

    public async Task<ScheduleDependencyResponse> AddDependencyAsync(AddScheduleDependencyRequest r, UserAccessContext u, CancellationToken ct)
    {
        EnsureManage(u); Required(r.PredecessorNumeroOT, nameof(r.PredecessorNumeroOT)); Required(r.SuccessorNumeroOT, nameof(r.SuccessorNumeroOT));
        if (Code(r.PredecessorNumeroOT) == Code(r.SuccessorNumeroOT)) throw new DomainException("Una OT no puede depender de si misma.");
        var schedules = await _db.WorkOrderSchedules.Include(x => x.WorkOrder).ThenInclude(x => x.Faena).Where(x => x.WorkOrder.WorkOrderNumber == Code(r.PredecessorNumeroOT) || x.WorkOrder.WorkOrderNumber == Code(r.SuccessorNumeroOT)).ToListAsync(ct);
        var predecessor = schedules.SingleOrDefault(x => x.WorkOrder.WorkOrderNumber == Code(r.PredecessorNumeroOT)) ?? throw new DomainException("La OT predecesora no esta programada."); var successor = schedules.SingleOrDefault(x => x.WorkOrder.WorkOrderNumber == Code(r.SuccessorNumeroOT)) ?? throw new DomainException("La OT sucesora no esta programada."); EnsureAccess(u, predecessor.WorkOrder.Faena.Code); EnsureAccess(u, successor.WorkOrder.Faena.Code);
        if (await _db.ScheduleDependencies.AnyAsync(x => x.PredecessorScheduleId == predecessor.Id && x.SuccessorScheduleId == successor.Id, ct)) throw new DomainException("La dependencia ya existe.");
        if (await HasPathAsync(successor.Id, predecessor.Id, ct)) throw new DomainException("La dependencia generaria un ciclo.");
        var entity = new ScheduleDependencyEntity { PredecessorScheduleId = predecessor.Id, SuccessorScheduleId = successor.Id, Type = Text(r.Tipo) ?? "FinishToStart", Reason = Text(r.Motivo), CreatedByUserId = u.UserId }; _db.ScheduleDependencies.Add(entity); await _db.SaveChangesAsync(ct); return new(entity.Id.ToString("N"), predecessor.WorkOrder.WorkOrderNumber, successor.WorkOrder.WorkOrderNumber, entity.Type, entity.Reason);
    }

    public async Task<ScheduleBoardResponse> GetBoardAsync(ScheduleBoardQuery q, UserAccessContext u, CancellationToken ct)
    {
        EnsureView(u); var from = q.From ?? DateTimeOffset.UtcNow.AddDays(-7); var to = q.To ?? DateTimeOffset.UtcNow.AddDays(30); if (to < from) throw new DomainException("El rango de fechas no es valido.");
        var workshops = _db.Workshops.AsNoTracking().Include(x => x.SupervisorUser).Where(x => x.IsActive).AsQueryable(); if (!string.IsNullOrWhiteSpace(q.TallerCodigo)) workshops = workshops.Where(x => x.Code == Code(q.TallerCodigo)); var workshopList = await workshops.OrderBy(x => x.Code).ToListAsync(ct); var ids = workshopList.Select(x => x.Id).ToArray();
        var schedules = await _db.WorkOrderSchedules.AsNoTracking().Include(x => x.Workshop).Include(x => x.WorkOrder).ThenInclude(x => x.Faena).Include(x => x.WorkOrder).ThenInclude(x => x.Asset).Include(x => x.WorkOrder).ThenInclude(x => x.OperationalUnit).Include(x => x.WorkOrder).ThenInclude(x => x.Priority).Include(x => x.WorkOrder).ThenInclude(x => x.Criticality).Where(x => ids.Contains(x.WorkshopId) && x.StartsAtUtc <= to && x.EndsAtUtc >= from).ToListAsync(ct);
        schedules = schedules.Where(x => CanAccess(u, x.WorkOrder.Faena.Code) && (string.IsNullOrWhiteSpace(q.FaenaCodigo) || x.WorkOrder.Faena.Code == Code(q.FaenaCodigo)) && (q.IncludeClosed || x.Status != (int)ScheduleItemStatus.Completado)).ToList(); var items = schedules.Select(x => ToItem(x, x.WorkOrder, x.Workshop)).ToArray();
        var alerts = await AlertsAsync(ids, from, to, u, q.FaenaCodigo, ct); return new(await MapWorkshopsAsync(workshopList, ct), items, Load(workshopList, schedules, from, to), Enum.GetValues<ScheduleItemStatus>().Select(s => new KanbanColumnResponse(s, items.Where(x => x.Estado == s).ToArray())).ToArray(), [], alerts);
    }

    public async Task<IReadOnlyCollection<ScheduleAlertResponse>> ListAlertsAsync(ScheduleBoardQuery q, UserAccessContext u, CancellationToken ct) { EnsureView(u); var ids = await _db.Workshops.Where(x => x.IsActive && (string.IsNullOrWhiteSpace(q.TallerCodigo) || x.Code == Code(q.TallerCodigo))).Select(x => x.Id).ToArrayAsync(ct); return await AlertsAsync(ids, q.From ?? DateTimeOffset.MinValue, q.To ?? DateTimeOffset.MaxValue, u, q.FaenaCodigo, ct); }

    private async Task<IReadOnlyCollection<WorkshopResponse>> MapWorkshopsAsync(IEnumerable<WorkshopEntity> workshops, CancellationToken ct) { var items = workshops.ToArray(); var ids = items.Select(x => x.Id).ToArray(); var occupied = await _db.AssetPhysicalLocationPeriods.Where(x => x.ValidToUtc == null && x.WorkshopId.HasValue && ids.Contains(x.WorkshopId.Value)).GroupBy(x => x.WorkshopId!.Value).Select(x => new { x.Key, Count = x.Select(y => y.AssetId).Distinct().Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct); return items.Select(x => new WorkshopResponse(x.Code, x.Name, x.EquipmentCapacity, x.Commune, x.SupervisorUserId?.ToString("D"), x.SupervisorUser?.DisplayName, occupied.GetValueOrDefault(x.Id), x.IsActive)).ToArray(); }
    private async Task<AppUserEntity?> SupervisorAsync(string? id, CancellationToken ct) { if (string.IsNullOrWhiteSpace(id)) return null; if (!Guid.TryParse(id, out var userId)) throw new DomainException("El supervisor es invalido."); var user = await _db.Users.Include(x => x.Roles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == userId, ct) ?? throw new DomainException("El supervisor no existe."); if (!user.IsActive || user.IsLocked || !user.Roles.Any(x => x.IsActive && x.Role.IsActive && (x.Role.Code == AuthRoles.MaintenanceSupervisor || x.Role.Code == AuthRoles.Admin))) throw new DomainException("El responsable debe estar activo, desbloqueado y tener un rol vigente de Supervisor de Mantenimiento o Administrador."); return user; }
    private async Task<WorkOrderEntity> OrderAsync(string code, CancellationToken ct) => await _db.WorkOrders.Include(x => x.Asset).Include(x => x.OperationalUnit).Include(x => x.Faena).Include(x => x.Priority).Include(x => x.Criticality).SingleOrDefaultAsync(x => x.WorkOrderNumber == Code(code), ct) ?? throw new DomainException("La OT no existe.");
    private async Task<int> DailyLoadAsync(Guid workshopId, DateTimeOffset day, CancellationToken ct) { var start = new DateTimeOffset(day.UtcDateTime.Date, TimeSpan.Zero); return await _db.WorkOrderSchedules.CountAsync(x => x.WorkshopId == workshopId && x.StartsAtUtc < start.AddDays(1) && x.EndsAtUtc >= start, ct); }
    private static IReadOnlyCollection<WorkshopLoadResponse> Load(IEnumerable<WorkshopEntity> workshops, IEnumerable<WorkOrderScheduleEntity> schedules, DateTimeOffset from, DateTimeOffset to) { var result = new List<WorkshopLoadResponse>(); foreach (var w in workshops) for (var day = DateOnly.FromDateTime(from.UtcDateTime); day <= DateOnly.FromDateTime(to.UtcDateTime); day = day.AddDays(1)) { var count = schedules.Count(x => x.WorkshopId == w.Id && x.StartsAtUtc.Date <= day.ToDateTime(TimeOnly.MinValue) && x.EndsAtUtc.Date >= day.ToDateTime(TimeOnly.MinValue)); result.Add(new(w.Code, w.Name, day, w.EquipmentCapacity, count, count > w.EquipmentCapacity)); } return result; }
    private async Task<IReadOnlyCollection<ScheduleAlertResponse>> AlertsAsync(Guid[] ids, DateTimeOffset from, DateTimeOffset to, UserAccessContext u, string? faena, CancellationToken ct) => (await _db.ScheduleAlerts.AsNoTracking().Include(x => x.Workshop).Include(x => x.WorkOrder).ThenInclude(x => x.Faena).Include(x => x.Faena).Where(x => (x.WorkshopId == null || ids.Contains(x.WorkshopId.Value)) && x.RaisedAtUtc >= from && x.RaisedAtUtc <= to).ToListAsync(ct)).Where(x => x.WorkOrder?.Faena is { } f && CanAccess(u, f.Code) && (string.IsNullOrWhiteSpace(faena) || f.Code == Code(faena))).Select(x => ToAlert(x, x.Workshop, x.WorkOrder)).ToArray();
    private async Task<bool> HasPathAsync(Guid from, Guid target, CancellationToken ct) { var queue = new Queue<Guid>(); var seen = new HashSet<Guid>(); queue.Enqueue(from); while (queue.Count > 0) { var current = queue.Dequeue(); if (!seen.Add(current)) continue; if (current == target) return true; foreach (var next in await _db.ScheduleDependencies.Where(x => x.PredecessorScheduleId == current).Select(x => x.SuccessorScheduleId).ToListAsync(ct)) queue.Enqueue(next); } return false; }
    private static ScheduleItemResponse ToItem(WorkOrderScheduleEntity x, WorkOrderEntity o, WorkshopEntity w) => new(x.Id.ToString("N"), o.WorkOrderNumber, w.Code, w.Name, o.Faena.Code, o.Asset?.Code ?? o.OperationalUnit?.Code ?? string.Empty, o.Asset?.Name ?? o.OperationalUnit?.Name, x.TechnicianUserId, x.StartsAtUtc, x.EndsAtUtc, x.EstimatedLaborHours, (ScheduleItemStatus)x.Status, o.Priority?.Code ?? string.Empty, o.Criticality?.Code ?? string.Empty, o.Description);
    private static ScheduleAlertResponse ToAlert(ScheduleAlertEntity x, WorkshopEntity? w, WorkOrderEntity? o) => new(x.Id.ToString("N"), (ScheduleAlertType)x.Type, x.Severity, x.Message, w?.Code, o?.WorkOrderNumber, o?.Faena?.Code ?? x.Faena?.Code, x.RaisedAtUtc, x.IsResolved);
    private static string? Text(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim(); private static string? Code(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim().ToUpperInvariant(); private static void Required(string? x, string n) { if (string.IsNullOrWhiteSpace(x)) throw new DomainException($"El campo {n} es obligatorio."); } private static bool CanAccess(UserAccessContext u, string code) => u.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase) || u.Faenas.Contains(code, StringComparer.OrdinalIgnoreCase); private static void EnsureAccess(UserAccessContext u, string code) { if (!CanAccess(u, code)) throw new UnauthorizedAccessException("No tiene acceso a la faena."); } private static void EnsureView(UserAccessContext u) { if (!(u.Roles.Contains(AuthRoles.Admin,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Planner,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.MaintenanceSupervisor,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.FaenaViewer,StringComparer.OrdinalIgnoreCase))) throw new UnauthorizedAccessException("No tiene permisos para ver programacion."); } private static void EnsureManage(UserAccessContext u) { if (!(u.Roles.Contains(AuthRoles.Admin,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Planner,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.MaintenanceSupervisor,StringComparer.OrdinalIgnoreCase))) throw new UnauthorizedAccessException("No tiene permisos para gestionar programacion."); }
}