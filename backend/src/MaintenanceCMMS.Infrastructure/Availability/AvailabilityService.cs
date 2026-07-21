using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.MaintenanceTargets;
using MaintenanceCMMS.Application.Availability;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.MaintenanceTargets;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Availability;

/// <summary>Availability contracts and events persisted exclusively through relational EF entities.</summary>
public sealed class AvailabilityService : IAvailabilityService
{
    private readonly CmmsDbContext _db;
    private readonly IMaintenanceTargetService _maintenanceTargets;
    public AvailabilityService(CmmsDbContext dbContext, IMaintenanceTargetService? maintenanceTargets = null)
    {
        _db = dbContext;
        _maintenanceTargets = maintenanceTargets ?? new MaintenanceTargetService(dbContext);
    }

    public async Task<IReadOnlyCollection<AvailabilityContractResponse>> ListContractsAsync(AvailabilityContractQuery query, UserAccessContext user, CancellationToken ct)
    {
        EnsureView(user); var contracts = ContractQuery(); if (!query.IncludeInactive) contracts = contracts.Where(x => x.IsActive); if (!string.IsNullOrWhiteSpace(query.FaenaCodigo)) contracts = contracts.Where(x => x.Faena.Code == Code(query.FaenaCodigo)); if (!string.IsNullOrWhiteSpace(query.Cliente)) contracts = contracts.Where(x => EF.Functions.ILike(x.Client, $"%{query.Cliente.Trim()}%"));
        return (await contracts.OrderBy(x => x.Code).ToListAsync(ct)).Where(x => CanAccess(user, x.Faena.Code)).Select(ToContract).ToArray();
    }

    public async Task<AvailabilityContractResponse> UpsertContractAsync(UpsertAvailabilityContractRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureManage(user); Required(request.ContractCode, nameof(request.ContractCode)); Required(request.Nombre, nameof(request.Nombre)); Required(request.Cliente, nameof(request.Cliente)); Required(request.FaenaCodigo, nameof(request.FaenaCodigo)); if (request.HorasComprometidasDia <= 0) throw new DomainException("Las horas comprometidas deben ser mayores a cero."); if (request.DisponibilidadObjetivo is < 0 or > 1) throw new DomainException("La disponibilidad objetivo debe estar entre 0 y 1."); if (request.FechaFin < request.FechaInicio) throw new DomainException("La fecha de termino no puede ser anterior al inicio.");
        var faena = await _db.Faenas.SingleOrDefaultAsync(x => x.Code == Code(request.FaenaCodigo), ct) ?? throw new DomainException($"La faena '{request.FaenaCodigo}' no existe."); EnsureAccess(user, faena.Code);
        var contract = await ContractQuery().SingleOrDefaultAsync(x => x.Code == Code(request.ContractCode), ct);
        if (contract is null) { contract = new AvailabilityContractEntity { Code = Code(request.ContractCode)!, CreatedByUserId = user.UserId }; _db.AvailabilityContracts.Add(contract); }
        contract.Name = request.Nombre.Trim(); contract.Client = request.Cliente.Trim(); contract.FaenaId = faena.Id; contract.Faena = faena; contract.CommittedHoursPerDay = request.HorasComprometidasDia; contract.TargetAvailability = request.DisponibilidadObjetivo; contract.StartsAtUtc = request.FechaInicio; contract.EndsAtUtc = request.FechaFin; contract.ClientRules = Text(request.ReglasCliente); contract.IsActive = request.Activo; contract.UpdatedByUserId = user.UserId; contract.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct); return ToContract(contract);
    }

    public Task<AvailabilityContractAssetResponse> AssignAssetAsync(AssignContractAssetRequest request, UserAccessContext user, CancellationToken ct) =>
        AssignTargetAsync(new AssignContractTargetRequest(
            request.ContractCode,
            new MaintenanceTargetReference(MaintenanceTargetType.Asset, request.ActivoCodigo),
            request.Rol,
            request.FechaInicio,
            request.FechaFin,
            request.Activo,
            request.Reason), user, ct);

    public async Task<AvailabilityContractAssetResponse> AssignTargetAsync(AssignContractTargetRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureManage(user);
        Required(request.ContractCode, nameof(request.ContractCode));
        if (request.FechaFin < request.FechaInicio) throw new DomainException("La fecha de termino no puede ser anterior al inicio.");
        var contract = await ContractQuery().SingleOrDefaultAsync(x => x.Code == Code(request.ContractCode), ct)
            ?? throw new DomainException($"El contrato '{request.ContractCode}' no existe.");
        EnsureAccess(user, contract.Faena.Code);
        var target = await _maintenanceTargets.ResolveAsync(request.Objetivo, user, ct);
        if (target.FaenaId != contract.FaenaId) throw new DomainException("El objetivo debe pertenecer a la faena del contrato.");
        if (!target.ParticipaEnDisponibilidad) throw new DomainException("El objetivo seleccionado no participa en disponibilidad.");

        var currentAssignments = await _db.AvailabilityContractAssignments
            .Where(item => item.ContractId == contract.Id && item.IsActive)
            .ToArrayAsync(ct);
        if (target.OperationalUnitId is Guid unitId)
        {
            var componentIds = await _db.OperationalUnitComponents
                .Where(item => item.OperationalUnitId == unitId && item.RemovedAtUtc == null)
                .Select(item => item.AssetId)
                .ToArrayAsync(ct);
            if (currentAssignments.Any(item => item.AssetId.HasValue && componentIds.Contains(item.AssetId.Value)))
            {
                throw new DomainException("No se puede asignar una unidad mientras sus componentes están asignados al mismo contrato.");
            }
        }
        else if (target.AssetId is Guid assetId)
        {
            var mountedUnitIds = await _db.OperationalUnitComponents
                .Where(item => item.AssetId == assetId && item.RemovedAtUtc == null)
                .Select(item => item.OperationalUnitId)
                .ToArrayAsync(ct);
            if (currentAssignments.Any(item => item.OperationalUnitId.HasValue && mountedUnitIds.Contains(item.OperationalUnitId.Value)))
            {
                throw new DomainException("No se puede asignar un componente mientras su unidad operativa está asignada al mismo contrato.");
            }
        }

        var assignment = await _db.AvailabilityContractAssignments
            .Include(item => item.Asset).ThenInclude(asset => asset!.AssetTypeDefinition)
            .Include(item => item.OperationalUnit).ThenInclude(unit => unit!.OperationalUnitType)
            .Include(item => item.Contract).ThenInclude(item => item.Faena)
            .SingleOrDefaultAsync(item => item.ContractId == contract.Id &&
                (target.AssetId.HasValue ? item.AssetId == target.AssetId : item.OperationalUnitId == target.OperationalUnitId), ct);
        if (assignment is null)
        {
            assignment = new AvailabilityContractAssignmentEntity
            {
                ContractId = contract.Id,
                AssetId = target.AssetId,
                OperationalUnitId = target.OperationalUnitId,
                CreatedByUserId = user.UserId
            };
            _db.AvailabilityContractAssignments.Add(assignment);
        }
        assignment.Contract = contract;
        assignment.Role = (int)request.Rol;
        assignment.StartsAtUtc = request.FechaInicio;
        assignment.EndsAtUtc = request.FechaFin;
        assignment.IsActive = request.Activo;
        assignment.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await AssignmentResponseAsync(assignment.Id, ct);
    }
    public async Task<IReadOnlyCollection<AvailabilityEventResponse>> ListEventsAsync(AvailabilityEventQuery query, UserAccessContext user, CancellationToken ct)
    {
        EnsureView(user);
        var events = EventQuery();
        if (query.From is not null) events = events.Where(x => (x.EndsAtUtc ?? DateTimeOffset.MaxValue) >= query.From);
        if (query.To is not null) events = events.Where(x => x.StartsAtUtc <= query.To);
        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo)) events = events.Where(x => x.Contract.Faena.Code == Code(query.FaenaCodigo));
        if (!string.IsNullOrWhiteSpace(query.ContractCode)) events = events.Where(x => x.Contract.Code == Code(query.ContractCode));
        if (!string.IsNullOrWhiteSpace(query.ActivoCodigo)) events = events.Where(x => x.Asset != null && x.Asset.Code == Code(query.ActivoCodigo));
        if (query.TipoObjetivo == MaintenanceTargetType.Asset) events = events.Where(x => x.AssetId != null);
        if (query.TipoObjetivo == MaintenanceTargetType.OperationalUnit) events = events.Where(x => x.OperationalUnitId != null);
        if (!string.IsNullOrWhiteSpace(query.ObjetivoCodigo)) events = events.Where(x => (x.Asset != null && x.Asset.Code == Code(query.ObjetivoCodigo)) || (x.OperationalUnit != null && x.OperationalUnit.Code == Code(query.ObjetivoCodigo)));
        if (query.Cause is not null) events = events.Where(x => x.Cause == (int)query.Cause.Value);
        return (await events.OrderByDescending(x => x.StartsAtUtc).ToListAsync(ct))
            .Where(x => CanAccess(user, x.Contract.Faena.Code)).Select(ToEvent).ToArray();
    }

    public async Task<AvailabilityEventResponse> RegisterEventAsync(RegisterAvailabilityEventRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureManage(user);
        Required(request.ContractCode, nameof(request.ContractCode));
        if (request.FinUtc < request.InicioUtc) throw new DomainException("El termino del evento no puede ser anterior al inicio.");
        var reference = MaintenanceTargetRequestNormalizer.Normalize(request.Objetivo, request.ActivoCodigo, null);
        var contract = await ContractQuery().SingleOrDefaultAsync(x => x.Code == Code(request.ContractCode), ct)
            ?? throw new DomainException($"El contrato '{request.ContractCode}' no existe.");
        EnsureAccess(user, contract.Faena.Code);
        var target = await _maintenanceTargets.ResolveAsync(reference!, user, ct);
        if (target.FaenaId != contract.FaenaId) throw new DomainException("El objetivo debe pertenecer a la faena del contrato.");
        if (!target.ParticipaEnDisponibilidad) throw new DomainException("El objetivo seleccionado no participa en disponibilidad.");
        var assignment = await _db.AvailabilityContractAssignments.SingleOrDefaultAsync(x => x.ContractId == contract.Id && x.IsActive &&
            (target.AssetId.HasValue ? x.AssetId == target.AssetId : x.OperationalUnitId == target.OperationalUnitId), ct)
            ?? throw new DomainException("El objetivo no esta asignado al contrato.");
        var workOrder = string.IsNullOrWhiteSpace(request.NumeroOT) ? null : await _db.WorkOrders.SingleOrDefaultAsync(x => x.WorkOrderNumber == Code(request.NumeroOT), ct);
        var entity = new AvailabilityEventEntity
        {
            ContractId = contract.Id, ContractAssignmentId = assignment.Id,
            AssetId = target.AssetId, OperationalUnitId = target.OperationalUnitId,
            WorkOrderId = workOrder?.Id, Cause = (int)request.Causa,
            StartsAtUtc = request.InicioUtc, EndsAtUtc = request.FinUtc,
            CanBeUsed = request.PuedeUtilizarse, IsMaintenanceAttributable = request.AtribuibleMantenimiento,
            Comment = Text(request.Comentario), CreatedByUserId = user.UserId
        };
        _db.AvailabilityEvents.Add(entity);
        await _db.SaveChangesAsync(ct);
        entity.Contract = contract;
        entity.WorkOrder = workOrder;
        if (target.AssetId is Guid assetId) entity.Asset = await _db.Assets.SingleAsync(item => item.Id == assetId, ct);
        if (target.OperationalUnitId is Guid unitId) entity.OperationalUnit = await _db.OperationalUnits.SingleAsync(item => item.Id == unitId, ct);
        return ToEvent(entity);
    }

    public async Task<AvailabilityDashboardResponse> GetDashboardAsync(AvailabilityQuery query, UserAccessContext user, CancellationToken ct)
    {
        EnsureView(user); var from = query.From ?? DateTimeOffset.UtcNow.AddMonths(-1); var to = query.To ?? DateTimeOffset.UtcNow; if (to <= from) throw new DomainException("El rango de fechas no es valido.");
        var contractsQuery = ContractQuery().Where(x => x.IsActive); if (!string.IsNullOrWhiteSpace(query.FaenaCodigo)) contractsQuery = contractsQuery.Where(x => x.Faena.Code == Code(query.FaenaCodigo)); if (!string.IsNullOrWhiteSpace(query.ContractCode)) contractsQuery = contractsQuery.Where(x => x.Code == Code(query.ContractCode)); if (!string.IsNullOrWhiteSpace(query.Cliente)) contractsQuery = contractsQuery.Where(x => EF.Functions.ILike(x.Client, $"%{query.Cliente.Trim()}%"));
        var contracts = (await contractsQuery.ToListAsync(ct)).Where(x => CanAccess(user, x.Faena.Code)).ToArray(); var ids = contracts.Select(x => x.Id).ToArray(); var events = await EventQuery().Where(x => ids.Contains(x.ContractId) && x.StartsAtUtc <= to && (x.EndsAtUtc ?? to) >= from).ToListAsync(ct); var summaries = contracts.Select(c => Summarize(c, events.Where(e => e.ContractId == c.Id), from, to)).ToArray(); var allEvents = events.Select(ToEvent).ToArray();
        var totalCommittedAssets = summaries.Sum(x => x.EquiposComprometidos); var covered = summaries.Sum(x => x.EquiposCubiertos); var committedHours = summaries.Sum(x => x.HorasComprometidas); var availableHours = summaries.Sum(x => x.HorasDisponibles); var unavailable = Math.Max(0, committedHours - availableHours); var target = summaries.Length == 0 ? 0 : summaries.Average(x => x.DisponibilidadObjetivo); var quantity = totalCommittedAssets == 0 ? 1 : (decimal)covered / totalCommittedAssets; var hours = committedHours == 0 ? 1 : availableHours / committedHours;
        var causes = events.GroupBy(x => (AvailabilityCause)x.Cause).Select(g => new AvailabilityCauseSummary(g.Key, g.Sum(x => OverlapHours(x.StartsAtUtc, x.EndsAtUtc, from, to)), g.Count(), g.Any(x => !x.CanBeUsed && x.IsMaintenanceAttributable))).OrderByDescending(x => x.HorasNoDisponibles).ToArray(); var unavailableAssets = events.Where(x => !x.CanBeUsed).Select(x => new UnavailableAssetResponse(x.Contract.Code, x.Asset?.Code ?? x.OperationalUnit?.Code ?? string.Empty, x.Asset?.Name ?? x.OperationalUnit?.Name, x.Contract.Faena.Code, (AvailabilityCause)x.Cause, x.StartsAtUtc, x.EndsAtUtc, OverlapHours(x.StartsAtUtc, x.EndsAtUtc, from, to), x.IsMaintenanceAttributable, false, x.WorkOrder?.WorkOrderNumber)).ToArray();
        var kpi = new AvailabilityKpiResponse(totalCommittedAssets, covered, Math.Max(0, totalCommittedAssets-covered), committedHours, availableHours, unavailable, quantity, hours, target, hours >= target); var byFaena = summaries.GroupBy(x => x.FaenaCodigo).Select(g => new AvailabilityFaenaSummary(g.Key, g.Sum(x => x.EquiposComprometidos), g.Sum(x => x.EquiposCubiertos), g.Sum(x => x.EquiposComprometidos) == 0 ? 1 : (decimal)g.Sum(x => x.EquiposCubiertos) / g.Sum(x => x.EquiposComprometidos), g.Sum(x => x.HorasComprometidas) == 0 ? 1 : g.Sum(x => x.HorasDisponibles) / g.Sum(x => x.HorasComprometidas))).ToArray(); var trends = new[] { new AvailabilityTrendPoint($"{from:yyyy-MM-dd}/{to:yyyy-MM-dd}", from, to, quantity, hours, totalCommittedAssets, covered, committedHours, availableHours) };
        return new(kpi, summaries, byFaena, causes, unavailableAssets, trends, allEvents);
    }

    private IQueryable<AvailabilityContractEntity> ContractQuery() => _db.AvailabilityContracts.Include(x => x.Faena).Include(x => x.Assignments).ThenInclude(x => x.Asset).Include(x => x.Assignments).ThenInclude(x => x.OperationalUnit);
    private IQueryable<AvailabilityEventEntity> EventQuery() => _db.AvailabilityEvents.Include(x => x.Contract).ThenInclude(x => x.Faena).Include(x => x.Asset).Include(x => x.OperationalUnit).Include(x => x.WorkOrder);
    private static AvailabilityContractResponse ToContract(AvailabilityContractEntity x) => new(x.Code, x.Name, x.Client, x.Faena.Code, x.CommittedHoursPerDay, x.TargetAvailability, x.StartsAtUtc, x.EndsAtUtc, x.ClientRules, x.IsActive, x.Assignments.Select(ToAssignment).ToArray());
    private static AvailabilityContractAssetResponse ToAssignment(AvailabilityContractAssignmentEntity x) => new(x.Id.ToString("N"), x.Contract.Code, x.Asset?.Code ?? x.OperationalUnit?.Code ?? string.Empty, x.Asset?.Name ?? x.OperationalUnit?.Name, x.Contract.Faena.Code, (ContractAssetRole)x.Role, x.StartsAtUtc, x.EndsAtUtc, x.IsActive, ToTargetReference(x.AssetId, x.Asset?.Code, x.OperationalUnitId, x.OperationalUnit?.Code));
    private static AvailabilityEventResponse ToEvent(AvailabilityEventEntity x) => new(x.Id.ToString("N"), x.Contract.Code, x.Asset?.Code ?? x.OperationalUnit?.Code ?? string.Empty, x.Asset?.Name ?? x.OperationalUnit?.Name, x.Contract.Faena.Code, (AvailabilityCause)x.Cause, x.StartsAtUtc, x.EndsAtUtc, x.CanBeUsed, x.IsMaintenanceAttributable, !x.CanBeUsed && x.IsMaintenanceAttributable, x.WorkOrder?.WorkOrderNumber, x.Comment, x.CreatedByUserId, x.CreatedAtUtc, ToTargetReference(x.AssetId, x.Asset?.Code, x.OperationalUnitId, x.OperationalUnit?.Code));
    private async Task<AvailabilityContractAssetResponse> AssignmentResponseAsync(Guid id, CancellationToken ct)
    {
        var assignment = await _db.AvailabilityContractAssignments
            .Include(item => item.Contract).ThenInclude(item => item.Faena)
            .Include(item => item.Asset)
            .Include(item => item.OperationalUnit)
            .SingleAsync(item => item.Id == id, ct);
        return ToAssignment(assignment);
    }

    private static MaintenanceTargetReference? ToTargetReference(Guid? assetId, string? assetCode, Guid? unitId, string? unitCode) =>
        unitId.HasValue && !string.IsNullOrWhiteSpace(unitCode)
            ? new MaintenanceTargetReference(MaintenanceTargetType.OperationalUnit, unitCode)
            : assetId.HasValue && !string.IsNullOrWhiteSpace(assetCode)
                ? new MaintenanceTargetReference(MaintenanceTargetType.Asset, assetCode)
                : null;
    private static AvailabilityContractSummary Summarize(AvailabilityContractEntity c, IEnumerable<AvailabilityEventEntity> events, DateTimeOffset from, DateTimeOffset to) { var assignments = c.Assignments.Where(x => x.IsActive && x.Role == (int)ContractAssetRole.Comprometido).ToArray(); var unavailable = events.Where(x => !x.CanBeUsed && x.IsMaintenanceAttributable).Select(TargetKey).ToHashSet(StringComparer.Ordinal); var covered = assignments.Count(x => !unavailable.Contains(TargetKey(x))); var committed = assignments.Length * c.CommittedHoursPerDay * (decimal)(to - from).TotalDays; var down = events.Where(x => !x.CanBeUsed && x.IsMaintenanceAttributable).Sum(x => OverlapHours(x.StartsAtUtc, x.EndsAtUtc, from, to)); var available = Math.Max(0, committed - down); return new(c.Code, c.Name, c.Client, c.Faena.Code, assignments.Length, covered, committed, available, assignments.Length == 0 ? 1 : (decimal)covered / assignments.Length, committed == 0 ? 1 : available / committed, c.TargetAvailability, (committed == 0 ? 1 : available / committed) >= c.TargetAvailability); }
    private static string TargetKey(AvailabilityContractAssignmentEntity assignment) => assignment.OperationalUnitId is Guid unitId ? $"OperationalUnit:{unitId:D}" : $"Asset:{assignment.AssetId:D}";
    private static string TargetKey(AvailabilityEventEntity item) => item.OperationalUnitId is Guid unitId ? $"OperationalUnit:{unitId:D}" : $"Asset:{item.AssetId:D}";
    private static decimal OverlapHours(DateTimeOffset start, DateTimeOffset? end, DateTimeOffset from, DateTimeOffset to) { var s = start < from ? from : start; var e = (end ?? to) > to ? to : (end ?? to); return e <= s ? 0 : (decimal)(e-s).TotalHours; }
    private static string? Text(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim(); private static string? Code(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim().ToUpperInvariant(); private static void Required(string? x, string n) { if (string.IsNullOrWhiteSpace(x)) throw new DomainException($"El campo {n} es obligatorio."); }
    private static bool CanAccess(UserAccessContext u, string code) => u.Roles.Contains(AuthRoles.Admin,StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management,StringComparer.OrdinalIgnoreCase) || u.Faenas.Contains(code,StringComparer.OrdinalIgnoreCase); private static void EnsureAccess(UserAccessContext u,string code){if(!CanAccess(u,code))throw new UnauthorizedAccessException("No tiene acceso a la faena.");} private static void EnsureView(UserAccessContext u){if(!(u.Roles.Contains(AuthRoles.Admin,StringComparer.OrdinalIgnoreCase)||u.Roles.Contains(AuthRoles.Planner,StringComparer.OrdinalIgnoreCase)||u.Roles.Contains(AuthRoles.MaintenanceSupervisor,StringComparer.OrdinalIgnoreCase)||u.Roles.Contains(AuthRoles.Management,StringComparer.OrdinalIgnoreCase)||u.Roles.Contains(AuthRoles.FaenaViewer,StringComparer.OrdinalIgnoreCase)))throw new UnauthorizedAccessException("No tiene permisos para ver disponibilidad.");} private static void EnsureManage(UserAccessContext u){if(!(u.Roles.Contains(AuthRoles.Admin,StringComparer.OrdinalIgnoreCase)||u.Roles.Contains(AuthRoles.Planner,StringComparer.OrdinalIgnoreCase)||u.Roles.Contains(AuthRoles.MaintenanceSupervisor,StringComparer.OrdinalIgnoreCase)))throw new UnauthorizedAccessException("No tiene permisos para gestionar disponibilidad.");}
}
