using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.MaintenanceTargets;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.MaintenanceTargets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class MaintenanceTargetServiceTests
{
    private static readonly UserAccessContext Planner = new("planner", [AuthRoles.Planner], [], ["FAE-1"]);

    [Fact]
    public async Task OperationalScope_ListsUnitAndIndependentAsset_ButNotMountedComponent()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        await SeedUnitAsync(fixture);
        var service = new MaintenanceTargetService(fixture.DbContext);

        var operational = await service.ListAsync(new MaintenanceTargetQuery(), Planner, CancellationToken.None);
        var all = await service.ListAsync(new MaintenanceTargetQuery(Scope: MaintenanceTargetScope.All), Planner, CancellationToken.None);

        Assert.Contains(operational, item => item.Tipo == MaintenanceTargetType.OperationalUnit && item.Codigo == "UNIT-1");
        Assert.Contains(operational, item => item.Tipo == MaintenanceTargetType.Asset && item.Codigo == "ACT-1");
        Assert.DoesNotContain(operational, item => item.Codigo == "ACT-2");
        var mounted = Assert.Single(all.Where(item => item.Codigo == "ACT-2"));
        Assert.True(mounted.EsComponenteMontado);
        Assert.Equal("UNIT-1", mounted.UnidadOperativaVigenteCodigo);
        Assert.Equal("CHASIS", mounted.RolComponenteVigente);
    }

    [Fact]
    public async Task Resolve_ReturnsExactlyOnePrimaryForeignKey_AndEnforcesFaenaAccess()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        await SeedUnitAsync(fixture);
        var service = new MaintenanceTargetService(fixture.DbContext);

        var asset = await service.ResolveAsync(new(MaintenanceTargetType.Asset, "act-1"), Planner, CancellationToken.None);
        var unit = await service.ResolveAsync(new(MaintenanceTargetType.OperationalUnit, "unit-1"), Planner, CancellationToken.None);

        Assert.True(asset.AssetId.HasValue);
        Assert.False(asset.OperationalUnitId.HasValue);
        Assert.False(unit.AssetId.HasValue);
        Assert.True(unit.OperationalUnitId.HasValue);
        var otherFaena = new UserAccessContext("viewer", [AuthRoles.FaenaViewer], [], ["FAE-2"]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ResolveAsync(new(MaintenanceTargetType.Asset, "ACT-1"), otherFaena, CancellationToken.None));
    }

    private static async Task SeedUnitAsync(PostgreSqlWorkTestFixture fixture)
    {
        var db = fixture.DbContext;
        if (await db.OperationalUnits.AnyAsync()) return;
        var faena = await db.Faenas.SingleAsync(item => item.Code == "FAE-1");
        var state = await db.AssetOperationalStates.SingleAsync(item => item.Code == "OPERATIVO_FAENA");
        var type = new OperationalUnitTypeEntity { Code = "CAMION", Name = "Camión fábrica", ParticipatesInAvailability = true };
        var role = new OperationalUnitComponentRoleEntity { Code = "CHASIS", Name = "Chasis" };
        var unit = new OperationalUnitEntity { Code = "UNIT-1", Name = "Camión fábrica 01", OperationalUnitType = type, Faena = faena, OperationalState = state };
        var component = new OperationalUnitComponentEntity
        {
            OperationalUnit = unit,
            AssetId = (await db.Assets.SingleAsync(item => item.Code == "ACT-2")).Id,
            ComponentRole = role,
            InstalledAtUtc = DateTimeOffset.UtcNow
        };
        db.AddRange(type, role, unit, component);
        await db.SaveChangesAsync();
    }
}
