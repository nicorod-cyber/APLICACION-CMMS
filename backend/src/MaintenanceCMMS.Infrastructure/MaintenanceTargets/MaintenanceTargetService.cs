using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.MaintenanceTargets;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.MaintenanceTargets;

/// <summary>
/// Resolves the existing asset and operational-unit aggregates without introducing
/// a materialized maintenance-target table.
/// </summary>
public sealed class MaintenanceTargetService : IMaintenanceTargetService
{
    private readonly CmmsDbContext _db;

    public MaintenanceTargetService(CmmsDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<MaintenanceTargetSummary>> ListAsync(
        MaintenanceTargetQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var targetFaena = Code(query.FaenaCodigo);
        if (targetFaena is not null && !CanAccessFaena(user, targetFaena))
        {
            throw new UnauthorizedAccessException("No tiene acceso a la faena solicitada.");
        }

        var assets = query.Tipo is null or MaintenanceTargetType.Asset
            ? await AssetTargetsAsync(query, user, targetFaena, cancellationToken)
            : [];
        var units = query.Tipo is null or MaintenanceTargetType.OperationalUnit
            ? await OperationalUnitTargetsAsync(query, user, targetFaena, cancellationToken)
            : [];

        return assets.Concat(units)
            .Where(target => MatchesSearch(target, query.Search))
            .OrderBy(target => target.EsComponenteMontado ? 1 : 0)
            .ThenBy(target => target.Nombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.Tipo)
            .ToArray();
    }

    public async Task<ResolvedMaintenanceTarget> ResolveAsync(
        MaintenanceTargetReference reference,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var code = Normalize(reference);
        switch (reference.Tipo)
        {
            case MaintenanceTargetType.Asset:
            {
                var asset = await _db.Assets.AsNoTracking()
                    .Include(item => item.Faena)
                    .Include(item => item.OperationalState)
                    .Include(item => item.AssetTypeDefinition)
                    .SingleOrDefaultAsync(item => item.Code == code, cancellationToken)
                    ?? throw new DomainException($"El activo '{code}' no existe.");
                EnsureAccess(user, asset.Faena?.Code);
                return new ResolvedMaintenanceTarget(
                    MaintenanceTargetType.Asset, asset.Code, asset.Name, asset.Id, null,
                    asset.FaenaId, asset.Faena?.Code, asset.OperationalState.Code, asset.Criticality,
                    asset.AssetTypeDefinition.ParticipatesInAvailability);
            }
            case MaintenanceTargetType.OperationalUnit:
            {
                var unit = await _db.OperationalUnits.AsNoTracking()
                    .Include(item => item.Faena)
                    .Include(item => item.OperationalState)
                    .Include(item => item.OperationalUnitType)
                    .SingleOrDefaultAsync(item => item.Code == code, cancellationToken)
                    ?? throw new DomainException($"La unidad operativa '{code}' no existe.");
                EnsureAccess(user, unit.Faena?.Code);
                return new ResolvedMaintenanceTarget(
                    MaintenanceTargetType.OperationalUnit, unit.Code, unit.Name, null, unit.Id,
                    unit.FaenaId, unit.Faena?.Code, unit.OperationalState.Code, unit.Criticality,
                    unit.OperationalUnitType.ParticipatesInAvailability);
            }
            default:
                throw new DomainException("El tipo de objetivo de mantenimiento no es válido.");
        }
    }

    private async Task<IReadOnlyCollection<MaintenanceTargetSummary>> AssetTargetsAsync(
        MaintenanceTargetQuery query,
        UserAccessContext user,
        string? faenaCode,
        CancellationToken ct)
    {
        var data = await _db.Assets.AsNoTracking()
            .Include(item => item.AssetTypeDefinition)
            .Include(item => item.Family)
            .Include(item => item.Faena)
            .Include(item => item.OperationalState)
            .Where(item => faenaCode == null || (item.Faena != null && item.Faena.Code == faenaCode))
            .Where(item => query.IncluirDadosDeBaja || item.OperationalState.Code != "DADO_DE_BAJA")
            .ToArrayAsync(ct);
        var assetIds = data.Select(item => item.Id).ToArray();
        var mounts = assetIds.Length == 0 ? [] : await _db.OperationalUnitComponents.AsNoTracking()
            .Include(item => item.OperationalUnit).ThenInclude(item => item.Faena)
            .Include(item => item.ComponentRole)
            .Where(item => assetIds.Contains(item.AssetId) && item.RemovedAtUtc == null)
            .ToArrayAsync(ct);
        var mountByAsset = mounts
            .GroupBy(item => item.AssetId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.InstalledAtUtc).First());

        return data
            .Where(item => CanAccessFaena(user, item.Faena?.Code))
            .Select(item =>
            {
                mountByAsset.TryGetValue(item.Id, out var mount);
                return new MaintenanceTargetSummary(
                    MaintenanceTargetType.Asset,
                    item.Code,
                    item.Name,
                    item.Family?.Code ?? item.AssetTypeDefinition.Code,
                    item.Family?.Name ?? item.AssetTypeDefinition.Name,
                    item.Faena?.Code,
                    item.Faena?.Name,
                    item.OperationalState.Code,
                    item.OperationalState.Name,
                    item.Criticality,
                    false,
                    null,
                    mount is not null,
                    mount?.OperationalUnit.Code,
                    mount?.OperationalUnit.Name,
                    mount?.ComponentRole.Code,
                    item.AssetTypeDefinition.ParticipatesInAvailability);
            })
            .Where(item => query.Scope == MaintenanceTargetScope.All || !item.EsComponenteMontado)
            .Where(item => !query.SoloDisponibilidad || item.ParticipaEnDisponibilidad)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<MaintenanceTargetSummary>> OperationalUnitTargetsAsync(
        MaintenanceTargetQuery query,
        UserAccessContext user,
        string? faenaCode,
        CancellationToken ct)
    {
        var units = await _db.OperationalUnits.AsNoTracking()
            .Include(item => item.OperationalUnitType)
            .Include(item => item.Faena)
            .Include(item => item.OperationalState)
            .Where(item => faenaCode == null || (item.Faena != null && item.Faena.Code == faenaCode))
            .Where(item => query.IncluirDadosDeBaja || item.OperationalState.Code != "DADO_DE_BAJA")
            .ToArrayAsync(ct);
        var unitIds = units.Select(item => item.Id).ToArray();
        var typeIds = units.Select(item => item.OperationalUnitTypeId).Distinct().ToArray();
        var components = unitIds.Length == 0 ? [] : await _db.OperationalUnitComponents.AsNoTracking()
            .Where(item => unitIds.Contains(item.OperationalUnitId) && item.RemovedAtUtc == null)
            .ToArrayAsync(ct);
        var rules = typeIds.Length == 0 ? [] : await _db.OperationalUnitCompositionRules.AsNoTracking()
            .Where(item => typeIds.Contains(item.OperationalUnitTypeId) && item.IsActive)
            .ToArrayAsync(ct);

        return units
            .Where(item => CanAccessFaena(user, item.Faena?.Code))
            .Select(item =>
            {
                var current = components.Where(component => component.OperationalUnitId == item.Id).ToArray();
                var complete = rules.Where(rule => rule.OperationalUnitTypeId == item.OperationalUnitTypeId && rule.IsMandatory)
                    .All(rule => current.Count(component => component.ComponentRoleId == rule.ComponentRoleId) >= rule.MinimumQuantity);
                return new MaintenanceTargetSummary(
                    MaintenanceTargetType.OperationalUnit,
                    item.Code,
                    item.Name,
                    item.OperationalUnitType.Code,
                    item.OperationalUnitType.Name,
                    item.Faena?.Code,
                    item.Faena?.Name,
                    item.OperationalState.Code,
                    item.OperationalState.Name,
                    item.Criticality,
                    true,
                    complete,
                    false,
                    null,
                    null,
                    null,
                    item.OperationalUnitType.ParticipatesInAvailability);
            })
            .Where(item => !query.SoloDisponibilidad || item.ParticipaEnDisponibilidad)
            .ToArray();
    }

    private static bool MatchesSearch(MaintenanceTargetSummary target, string? rawSearch)
    {
        if (string.IsNullOrWhiteSpace(rawSearch)) return true;
        var search = rawSearch.Trim();
        return new[]
        {
            target.Nombre, target.Codigo, target.CategoriaCodigo, target.CategoriaNombre,
            target.FaenaCodigo, target.FaenaNombre, target.EstadoOperacionalCodigo,
            target.EstadoOperacionalNombre, target.UnidadOperativaVigenteNombre
        }.Any(value => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string Normalize(MaintenanceTargetReference reference)
    {
        if (!Enum.IsDefined(reference.Tipo)) throw new DomainException("El tipo de objetivo de mantenimiento no es válido.");
        if (string.IsNullOrWhiteSpace(reference.Codigo)) throw new DomainException("El código del objetivo de mantenimiento es obligatorio.");
        return reference.Codigo.Trim().ToUpperInvariant();
    }

    private static string? Code(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static bool AnyRole(UserAccessContext user, params string[] roles) => roles.Any(role => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
    private static void EnsureCanView(UserAccessContext user)
    {
        if (AnyRole(user, AuthRoles.Admin, AuthRoles.Management, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Technician, AuthRoles.FaenaViewer)) return;
        throw new UnauthorizedAccessException("No tiene permisos para consultar objetivos de mantenimiento.");
    }

    private static bool CanAccessFaena(UserAccessContext user, string? faenaCode) =>
        string.IsNullOrWhiteSpace(faenaCode) || AnyRole(user, AuthRoles.Admin, AuthRoles.Management) || user.Faenas.Contains(faenaCode, StringComparer.OrdinalIgnoreCase);

    private static void EnsureAccess(UserAccessContext user, string? faenaCode)
    {
        if (CanAccessFaena(user, faenaCode)) return;
        throw new UnauthorizedAccessException("No tiene acceso a la faena del objetivo de mantenimiento.");
    }
}
