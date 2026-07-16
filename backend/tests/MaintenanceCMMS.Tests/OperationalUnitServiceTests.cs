using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.OperationalUnits;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.OperationalUnits;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class OperationalUnitServiceTests
{
    private static readonly UserAccessContext Admin = new("admin", [AuthRoles.Admin], ["unidades_operativas.administrar", "unidades_operativas.composicion", "unidades_operativas.ver"], ["F001"]);

    [Fact]
    public async Task ReplaceComponent_PreservesHistoryAndNeverLeavesTwoCurrentFactories()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("CFA", "CFA"), Admin, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("CHASIS", "Chasis"), Admin, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("FABRICA", "F�brica"), Admin, CancellationToken.None);
        var permitted = new[] { new AllowedComponentRequest("MONTABLE") };
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "CHASIS", 1, 1, true, permitted), Admin, CancellationToken.None);
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "FABRICA", 1, 1, true, permitted), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-1000", "CFA 1000", "CFA", "F001", "OPERATIVO_FAENA"), Admin, CancellationToken.None);

        await fixture.Service.MountAsync("CFA-1000", new MountOperationalUnitComponentRequest("CHF-TWCK41", "CHASIS"), Admin, CancellationToken.None);
        var initial = await fixture.Service.MountAsync("CFA-1000", new MountOperationalUnitComponentRequest("AUGER-1000", "FABRICA"), Admin, CancellationToken.None);
        Assert.True(initial!.Completa);

        var replaced = await fixture.Service.ReplaceAsync("CFA-1000", new ReplaceOperationalUnitComponentRequest("AUGER-1000", "QUADRA-1020", "FABRICA"), Admin, CancellationToken.None);
        Assert.True(replaced!.Completa);
        Assert.Single(replaced.Vigentes.Where(x => x.RolComponenteCodigo == "FABRICA"));
        Assert.Equal("QUADRA-1020", replaced.Vigentes.Single(x => x.RolComponenteCodigo == "FABRICA").ActivoCodigo);
        Assert.Contains(replaced.Historial, x => x.ActivoCodigo == "AUGER-1000" && x.FechaDesmontajeUtc.HasValue);
    }

    [Fact]
    public async Task ViewPermission_RespectsFaenaScopeAndDoesNotGrantManagement()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("CFA", "CFA"), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-2000", "CFA 2000", "CFA", "F001", "OPERATIVO_FAENA"), Admin, CancellationToken.None);
        var viewer = new UserAccessContext("viewer", [AuthRoles.FaenaViewer], [AuthPermissions.ViewOperationalUnits], ["F001"]);
        var otherFaenaViewer = new UserAccessContext("viewer-2", [AuthRoles.FaenaViewer], [AuthPermissions.ViewOperationalUnits], ["F002"]);

        Assert.Single(await fixture.Service.ListAsync("F001", viewer, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.GetAsync("CFA-2000", otherFaenaViewer, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("NO", "No autorizado"), viewer, CancellationToken.None));
    }
    private sealed record Fixture(string DatabaseName, string AdminConnectionString, CmmsDbContext Db, IOperationalUnitService Service) : IAsyncDisposable
    {
        public static async Task<Fixture> CreateAsync()
        {
            var name = $"cmms_operational_unit_{Guid.NewGuid():N}";
            var adminConnectionString = await PostgreSqlWorkTestFixture.GetAdminConnectionStringAsync();
            await PostgreSqlWorkTestFixture.CreateDatabaseAsync(name, adminConnectionString);
            var db = new CmmsDbContext(new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(PostgreSqlWorkTestFixture.ConnectionString(adminConnectionString, name)).Options);
            await db.Database.MigrateAsync();
            var faena = new FaenaEntity { Code = "F001", Name = "Faena", IsActive = true }; var type = new AssetTypeEntity { Code = "MONTABLE", Name = "Montable", IsMountable = true, IsActive = true }; var state = new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo", IsActive = true };
            db.AddRange(
                faena,
                type,
                state,
                new TechnicalLocationEntity
                {
                    Code = "UT-F001",
                    Name = "Ubicación técnica Faena",
                    FaenaId = faena.Id,
                    Faena = faena,
                    IsObsolete = false
                });
            db.Assets.AddRange(new AssetEntity { Code = "CHF-TWCK41", Name = "Chasis", AssetTypeId = type.Id, Faena = faena, OperationalState = state }, new AssetEntity { Code = "AUGER-1000", Name = "Auger", AssetTypeId = type.Id, Faena = faena, OperationalState = state }, new AssetEntity { Code = "QUADRA-1020", Name = "Quadra", AssetTypeId = type.Id, Faena = faena, OperationalState = state }); await db.SaveChangesAsync();
            return new Fixture(name, adminConnectionString, db, new OperationalUnitService(db, new PostgreSqlAuditService(db, new AuditContextAccessor())));
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync(); await PostgreSqlWorkTestFixture.DropDatabaseAsync(DatabaseName, AdminConnectionString);
        }
    }
}
