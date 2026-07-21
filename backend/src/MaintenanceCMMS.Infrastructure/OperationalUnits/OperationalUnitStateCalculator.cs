using MaintenanceCMMS.Infrastructure.Assets;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.OperationalUnits;

internal static class OperationalUnitStateCalculator
{
    public static async Task RecalculateForAssetAsync(
        CmmsDbContext db,
        Guid assetId,
        string? antecedent,
        CancellationToken cancellationToken)
    {
        var units = await db.OperationalUnitComponents
            .Where(component => component.AssetId == assetId && component.RemovedAtUtc == null)
            .Select(component => component.OperationalUnit)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        foreach (var unit in units)
        {
            await RecalculateAsync(db, unit, antecedent, cancellationToken);
        }
    }

    public static async Task RecalculateAsync(
        CmmsDbContext db,
        OperationalUnitEntity unit,
        string? antecedent,
        CancellationToken cancellationToken)
    {
        var components = await db.OperationalUnitComponents
            .Include(component => component.Asset).ThenInclude(asset => asset.OperationalState)
            .Include(component => component.ComponentRole)
            .Where(component => component.OperationalUnitId == unit.Id && component.RemovedAtUtc == null)
            .ToArrayAsync(cancellationToken);

        var source = components
            .OrderByDescending(component => AssetOperationalPolicy.Severity(component.Asset.OperationalState))
            .ThenBy(component => component.ComponentRole.Code)
            .ThenBy(component => component.Asset.Code)
            .FirstOrDefault();

        if (source is null)
        {
            if (unit.BaselineOperationalStateId.HasValue)
            {
                unit.OperationalStateId = unit.BaselineOperationalStateId.Value;
            }

            unit.DerivedFromAssetId = null;
            unit.DerivedStateReason = "Sin componentes vigentes; se utiliza el estado base de la unidad.";
            unit.DerivedStateCalculatedAtUtc = DateTimeOffset.UtcNow;
            return;
        }

        unit.OperationalStateId = source.Asset.OperationalStateId;
        unit.DerivedFromAssetId = source.AssetId;
        unit.DerivedStateReason = string.IsNullOrWhiteSpace(antecedent)
            ? $"Estado restringido por {source.ComponentRole.Code} {source.Asset.Code} ({source.Asset.OperationalState.Code})."
            : $"Estado restringido por {source.ComponentRole.Code} {source.Asset.Code} ({source.Asset.OperationalState.Code}). Antecedente: {antecedent.Trim()}";
        unit.DerivedStateCalculatedAtUtc = DateTimeOffset.UtcNow;
    }
}
