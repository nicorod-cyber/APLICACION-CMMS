using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.OperationalUnits;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.OperationalUnits;

public sealed class OperationalUnitService(CmmsDbContext db, IAuditService audit) : IOperationalUnitService
{
    public async Task<IReadOnlyCollection<OperationalUnitResponse>> ListAsync(string? faenaCodigo, UserAccessContext user, CancellationToken ct)
    {
        View(user);
        var units = await Units().Where(x => string.IsNullOrWhiteSpace(faenaCodigo) || x.Faena!.Code == Code(faenaCodigo)).ToListAsync(ct);
        var result = new List<OperationalUnitResponse>();
        foreach (var unit in units.Where(x => CanView(user, x))) result.Add(await MapAsync(unit, ct));
        return result.OrderBy(x => x.Codigo).ToArray();
    }

    public async Task<OperationalUnitResponse?> GetAsync(string codigo, UserAccessContext user, CancellationToken ct)
    {
        View(user); var unit = await FindUnitAsync(codigo, ct); if (unit is null) return null; EnsureView(user, unit); return await MapAsync(unit, ct);
    }

    public async Task<OperationalUnitTypeRequest> CreateTypeAsync(OperationalUnitTypeRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageUnits(u); Require(r.Codigo, "Codigo"); Require(r.Nombre, "Nombre"); var code = Code(r.Codigo);
        if (await db.OperationalUnitTypes.AnyAsync(x => x.Code == code, ct)) throw new DomainException("El tipo de unidad ya existe.");
        db.OperationalUnitTypes.Add(new OperationalUnitTypeEntity { Code = code, Name = r.Nombre.Trim(), Description = Text(r.Descripcion), ParticipatesInAvailability = r.ParticipaEnDisponibilidad, IsActive = true }); await db.SaveChangesAsync(ct);
        return r with { Codigo = code, Nombre = r.Nombre.Trim(), Descripcion = Text(r.Descripcion) };
    }

    public async Task<OperationalUnitRoleRequest> CreateRoleAsync(OperationalUnitRoleRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageUnits(u); Require(r.Codigo, "Codigo"); Require(r.Nombre, "Nombre"); var code = Code(r.Codigo);
        if (await db.OperationalUnitComponentRoles.AnyAsync(x => x.Code == code, ct)) throw new DomainException("El rol de componente ya existe.");
        db.OperationalUnitComponentRoles.Add(new OperationalUnitComponentRoleEntity { Code = code, Name = r.Nombre.Trim(), Description = Text(r.Descripcion), IsActive = true }); await db.SaveChangesAsync(ct);
        return r with { Codigo = code, Nombre = r.Nombre.Trim(), Descripcion = Text(r.Descripcion) };
    }

    public async Task<OperationalUnitRuleResponse> UpsertRuleAsync(OperationalUnitRuleRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageComposition(u); if (r.CantidadMinima < 0 || r.CantidadMaxima < r.CantidadMinima) throw new DomainException("Las cantidades de la regla son invalidas."); if (r.Obligatorio && r.CantidadMinima == 0) throw new DomainException("Una regla obligatoria debe exigir al menos un componente.");
        var type = await db.OperationalUnitTypes.SingleOrDefaultAsync(x => x.Code == Code(r.TipoUnidadCodigo) && x.IsActive, ct) ?? throw new DomainException("Tipo de unidad inexistente.");
        var role = await db.OperationalUnitComponentRoles.SingleOrDefaultAsync(x => x.Code == Code(r.RolComponenteCodigo) && x.IsActive, ct) ?? throw new DomainException("Rol inexistente.");
        var rule = await db.OperationalUnitCompositionRules.Include(x => x.AllowedAssets).SingleOrDefaultAsync(x => x.OperationalUnitTypeId == type.Id && x.ComponentRoleId == role.Id, ct);
        if (rule is null) { rule = new OperationalUnitCompositionRuleEntity { OperationalUnitTypeId = type.Id, ComponentRoleId = role.Id }; db.OperationalUnitCompositionRules.Add(rule); }
        rule.MinimumQuantity = r.CantidadMinima; rule.MaximumQuantity = r.CantidadMaxima; rule.IsMandatory = r.Obligatorio; rule.IsActive = true;
        db.OperationalUnitCompositionRuleAllowedAssets.RemoveRange(rule.AllowedAssets);
        var allowed = new List<AllowedComponentRequest>();
        foreach (var item in r.Permitidos ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.TipoActivoCodigo) && string.IsNullOrWhiteSpace(item.FamiliaEquipoCodigo)) throw new DomainException("Cada permitido debe indicar tipo o familia.");
            var at = string.IsNullOrWhiteSpace(item.TipoActivoCodigo) ? null : await db.AssetTypes.SingleOrDefaultAsync(x => x.Code == Code(item.TipoActivoCodigo) && x.IsActive, ct) ?? throw new DomainException("Tipo de activo permitido inexistente.");
            var family = string.IsNullOrWhiteSpace(item.FamiliaEquipoCodigo) ? null : await db.EquipmentFamilies.SingleOrDefaultAsync(x => x.Code == Code(item.FamiliaEquipoCodigo) && x.IsActive, ct) ?? throw new DomainException("Familia permitida inexistente.");
            if (at is not null && family is not null && family.AssetTypeId != at.Id) throw new DomainException("La familia permitida no corresponde al tipo indicado.");
            if (allowed.Any(x => Same(x.TipoActivoCodigo, at?.Code) && Same(x.FamiliaEquipoCodigo, family?.Code))) continue;
            db.OperationalUnitCompositionRuleAllowedAssets.Add(new OperationalUnitCompositionRuleAllowedAssetEntity { OperationalUnitCompositionRule = rule, AssetTypeId = at?.Id, EquipmentFamilyId = family?.Id });
            allowed.Add(new AllowedComponentRequest(at?.Code, family?.Code));
        }
        await db.SaveChangesAsync(ct); return new(type.Code, role.Code, rule.MinimumQuantity, rule.MaximumQuantity, rule.IsMandatory, allowed);
    }

    public async Task<OperationalUnitResponse> CreateAsync(OperationalUnitRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageUnits(u); Require(r.Codigo, "Codigo"); Require(r.Nombre, "Nombre"); var code = Code(r.Codigo); if (await db.OperationalUnits.AnyAsync(x => x.Code == code, ct)) throw new DomainException("La unidad operativa ya existe.");
        var type = await db.OperationalUnitTypes.SingleOrDefaultAsync(x => x.Code == Code(r.TipoUnidadCodigo) && x.IsActive, ct) ?? throw new DomainException("Tipo de unidad inexistente.");
        var state = await db.AssetOperationalStates.SingleOrDefaultAsync(x => x.Code == Code(r.EstadoOperacionalCodigo) && x.IsActive, ct) ?? throw new DomainException("Estado operacional inexistente.");
        var faena = string.IsNullOrWhiteSpace(r.FaenaCodigo) ? null : await db.Faenas.Include(x => x.TechnicalLocation).SingleOrDefaultAsync(x => x.Code == Code(r.FaenaCodigo) && x.IsActive, ct) ?? throw new DomainException("Faena inexistente.");
        if (faena is not null) EnsureView(u, faena.Code);
        if (faena is not null && faena.TechnicalLocation is null) throw new DomainException("La faena indicada no tiene una ubicación técnica configurada.");
        if (r.FechaBaja is { } end && r.FechaPuestaServicio is { } start && end < start) throw new DomainException("La baja no puede preceder a la puesta en servicio.");
        var unit = new OperationalUnitEntity { Code = code, Name = r.Nombre.Trim(), OperationalUnitTypeId = type.Id, FaenaId = faena?.Id, OperationalStateId = state.Id, Criticality = Text(r.Criticidad), CommissioningDate = r.FechaPuestaServicio, DecommissioningDate = r.FechaBaja, Observations = Text(r.Observaciones) };
        db.OperationalUnits.Add(unit); await db.SaveChangesAsync(ct); await Audit(u, "operational_unit.created", unit.Code, faena?.Code, ct); return (await GetAsync(unit.Code, u, ct))!;
    }

    public async Task<OperationalUnitCompositionResponse?> MountAsync(string unidadCodigo, MountOperationalUnitComponentRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageComposition(u); var unit = await FindUnitAsync(unidadCodigo, ct); if (unit is null) return null; EnsureView(u, unit); await using var tx = await db.Database.BeginTransactionAsync(ct); await MountCoreAsync(unit, r.ActivoCodigo, r.RolComponenteCodigo, r.OrdenTrabajoNumero, r.FechaMontajeUtc ?? DateTimeOffset.UtcNow, r.Observaciones, u, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await Audit(u, "operational_unit.component_mounted", unit.Code, unit.Faena?.Code, ct); return await CompositionAsync(unit, ct);
    }

    public async Task<OperationalUnitCompositionResponse?> UnmountAsync(string unidadCodigo, string activoCodigo, UnmountOperationalUnitComponentRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageComposition(u); var unit = await FindUnitAsync(unidadCodigo, ct); if (unit is null) return null; EnsureView(u, unit); var component = await db.OperationalUnitComponents.Include(x => x.Asset).SingleOrDefaultAsync(x => x.OperationalUnitId == unit.Id && x.Asset.Code == Code(activoCodigo) && x.RemovedAtUtc == null, ct) ?? throw new DomainException("El activo no est� montado en la unidad.");
        var date = r.FechaDesmontajeUtc ?? DateTimeOffset.UtcNow; if (date < component.InstalledAtUtc) throw new DomainException("La fecha de desmontaje no puede preceder el montaje."); component.RemovedAtUtc = date; component.RemovalWorkOrderId = await WorkOrderIdAsync(r.OrdenTrabajoNumero, ct); component.Observations = Text(r.Observaciones) ?? component.Observations; await db.SaveChangesAsync(ct); await Audit(u, "operational_unit.component_unmounted", unit.Code, unit.Faena?.Code, ct); return await CompositionAsync(unit, ct);
    }

    public async Task<OperationalUnitCompositionResponse?> ReplaceAsync(string unidadCodigo, ReplaceOperationalUnitComponentRequest r, UserAccessContext u, CancellationToken ct)
    {
        ManageComposition(u); var unit = await FindUnitAsync(unidadCodigo, ct); if (unit is null) return null; EnsureView(u, unit); await using var tx = await db.Database.BeginTransactionAsync(ct); var outgoing = await db.OperationalUnitComponents.Include(x => x.Asset).Include(x => x.ComponentRole).SingleOrDefaultAsync(x => x.OperationalUnitId == unit.Id && x.Asset.Code == Code(r.ActivoSalienteCodigo) && x.ComponentRole.Code == Code(r.RolComponenteCodigo) && x.RemovedAtUtc == null, ct) ?? throw new DomainException("El componente saliente no est� montado en el rol indicado."); var date = r.FechaOperacionUtc ?? DateTimeOffset.UtcNow; outgoing.RemovedAtUtc = date; outgoing.RemovalWorkOrderId = await WorkOrderIdAsync(r.OrdenTrabajoNumero, ct); await db.SaveChangesAsync(ct); await MountCoreAsync(unit, r.ActivoEntranteCodigo, r.RolComponenteCodigo, r.OrdenTrabajoNumero, date, r.Observaciones, u, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await Audit(u, "operational_unit.component_replaced", unit.Code, unit.Faena?.Code, ct); return await CompositionAsync(unit, ct);
    }

    private async Task MountCoreAsync(OperationalUnitEntity unit, string assetCode, string roleCode, string? workOrder, DateTimeOffset mountedAt, string? observations, UserAccessContext u, CancellationToken ct)
    {
        var role = await db.OperationalUnitComponentRoles.SingleOrDefaultAsync(x => x.Code == Code(roleCode) && x.IsActive, ct) ?? throw new DomainException("Rol de componente inexistente.");
        var rule = await db.OperationalUnitCompositionRules.Include(x => x.AllowedAssets).SingleOrDefaultAsync(x => x.OperationalUnitTypeId == unit.OperationalUnitTypeId && x.ComponentRoleId == role.Id && x.IsActive, ct) ?? throw new DomainException("No existe regla activa para el rol en este tipo de unidad.");
        var asset = await db.Assets.Include(x => x.AssetTypeDefinition).Include(x => x.Family).Include(x => x.OperationalState).Include(x => x.Faena).SingleOrDefaultAsync(x => x.Code == Code(assetCode), ct) ?? throw new DomainException("Activo inexistente.");
        if (!asset.AssetTypeDefinition.IsMountable) throw new DomainException("El activo no es montable."); if (asset.OperationalState.Code == "DADO_DE_BAJA") throw new DomainException("No se puede montar un activo dado de baja."); if (asset.FaenaId != unit.FaenaId) throw new DomainException("El activo y la unidad deben pertenecer a la misma faena.");
        if (await db.OperationalUnitComponents.AnyAsync(x => x.AssetId == asset.Id && x.RemovedAtUtc == null, ct)) throw new DomainException("El activo ya est� montado en otra unidad.");
        var allowed = rule.AllowedAssets; if (allowed.Count > 0 && !allowed.Any(x => x.AssetTypeId == asset.AssetTypeId || (asset.FamilyId.HasValue && x.EquipmentFamilyId == asset.FamilyId))) throw new DomainException("El tipo o familia del activo no est� permitido para el rol.");
        var count = await db.OperationalUnitComponents.CountAsync(x => x.OperationalUnitId == unit.Id && x.ComponentRoleId == role.Id && x.RemovedAtUtc == null, ct); if (count >= rule.MaximumQuantity) throw new DomainException("Se excede la cantidad m�xima del rol.");
        db.OperationalUnitComponents.Add(new OperationalUnitComponentEntity { OperationalUnitId = unit.Id, AssetId = asset.Id, ComponentRoleId = role.Id, InstalledAtUtc = mountedAt, InstallationWorkOrderId = await WorkOrderIdAsync(workOrder, ct), Observations = Text(observations) });
    }

    private IQueryable<OperationalUnitEntity> Units() => db.OperationalUnits.Include(x => x.OperationalUnitType).Include(x => x.Faena).ThenInclude(x => x.TechnicalLocation).Include(x => x.OperationalState);
    private Task<OperationalUnitEntity?> FindUnitAsync(string code, CancellationToken ct) => Units().SingleOrDefaultAsync(x => x.Code == Code(code), ct);
    private async Task<OperationalUnitCompositionResponse> CompositionAsync(OperationalUnitEntity unit, CancellationToken ct)
    {
        var rows = await db.OperationalUnitComponents.AsNoTracking().Include(x => x.Asset).Include(x => x.ComponentRole).Include(x => x.InstallationWorkOrder).Include(x => x.RemovalWorkOrder).Where(x => x.OperationalUnitId == unit.Id).OrderBy(x => x.InstalledAtUtc).ToListAsync(ct);
        var rules = await db.OperationalUnitCompositionRules.AsNoTracking().Include(x => x.ComponentRole).Where(x => x.OperationalUnitTypeId == unit.OperationalUnitTypeId && x.IsActive).ToListAsync(ct);
        var missing = rules.Where(x => x.IsMandatory && rows.Count(c => c.RemovedAtUtc is null && c.ComponentRoleId == x.ComponentRoleId) < x.MinimumQuantity).Select(x => x.ComponentRole.Code).ToArray();
        OperationalUnitComponentResponse Map(OperationalUnitComponentEntity x) => new(x.Asset.Code, x.Asset.Name, x.ComponentRole.Code, x.InstalledAtUtc, x.RemovedAtUtc, x.InstallationWorkOrder?.WorkOrderNumber, x.RemovalWorkOrder?.WorkOrderNumber, x.Observations);
        return new(missing.Length == 0, missing, rows.Where(x => x.RemovedAtUtc is null).Select(Map).ToArray(), rows.Select(Map).ToArray());
    }
    private async Task<OperationalUnitResponse> MapAsync(OperationalUnitEntity unit, CancellationToken ct) => new(unit.Code, unit.Name, unit.OperationalUnitType.Code, unit.Faena?.Code, unit.Faena?.TechnicalLocation?.Code, unit.OperationalState.Code, unit.Criticality, unit.CommissioningDate, unit.DecommissioningDate, unit.Observations, await CompositionAsync(unit, ct));
    private async Task<Guid?> WorkOrderIdAsync(string? number, CancellationToken ct) => string.IsNullOrWhiteSpace(number) ? null : (await db.WorkOrders.SingleOrDefaultAsync(x => x.WorkOrderNumber == Code(number), ct) ?? throw new DomainException("La OT indicada no existe.")).Id;
    private static void Require(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new DomainException($"{name} es obligatorio."); }
    private static string Code(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty; private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim(); private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool CanView(UserAccessContext u, OperationalUnitEntity unit) => unit.Faena is null || u.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase) || u.Faenas.Contains(unit.Faena.Code, StringComparer.OrdinalIgnoreCase);
    private static void View(UserAccessContext u) { if (u.Permissions.Contains(AuthPermissions.ViewOperationalUnits, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene permiso para ver unidades operativas."); }
    private static void ManageUnits(UserAccessContext u) { if (u.Permissions.Contains(AuthPermissions.ManageOperationalUnits, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene permiso para administrar unidades operativas."); }
    private static void ManageComposition(UserAccessContext u) { if (u.Permissions.Contains(AuthPermissions.ManageOperationalUnitComposition, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene permiso para administrar la composición de unidades operativas."); }
    private static void EnsureView(UserAccessContext u, OperationalUnitEntity unit) { if (!CanView(u, unit)) throw new UnauthorizedAccessException("No tiene acceso a la faena de la unidad operativa."); }
    private static void EnsureView(UserAccessContext u, string faena) { if (u.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase) || u.Faenas.Contains(faena, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene acceso a la faena."); }
    private Task Audit(UserAccessContext u, string action, string id, string? faena, CancellationToken ct) => audit.RecordAsync(new AuditEventRequest(u.UserId, action, "OperationalUnits", "OperationalUnit", id, null, null, faena, AuditSeverity.Medium), ct);
}

