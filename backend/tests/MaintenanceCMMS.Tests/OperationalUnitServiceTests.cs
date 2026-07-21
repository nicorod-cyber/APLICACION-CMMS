using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.OperationalUnits;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Assets;
using MaintenanceCMMS.Infrastructure.Security;
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
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("FABRICA", "Fï¿½brica"), Admin, CancellationToken.None);
        var permitted = new[] { new AllowedComponentRequest("MONTABLE") };
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "CHASIS", 1, 1, true, permitted), Admin, CancellationToken.None);
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "FABRICA", 1, 1, true, permitted), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-1000", "CFA 1000", "CFA", "F001", "OPERATIVO_FAENA"), Admin, CancellationToken.None);

        await fixture.Service.MountAsync("CFA-1000", new MountOperationalUnitComponentRequest("CHF-TWCK41", "CHASIS", Motivo: "Montaje inicial"), Admin, CancellationToken.None);
        var initial = await fixture.Service.MountAsync("CFA-1000", new MountOperationalUnitComponentRequest("AUGER-1000", "FABRICA", Motivo: "Montaje inicial"), Admin, CancellationToken.None);
        Assert.True(initial!.Completa);

        var replaced = await fixture.Service.ReplaceAsync("CFA-1000", new ReplaceOperationalUnitComponentRequest("AUGER-1000", "QUADRA-1020", "FABRICA", Motivo: "Renovacion de fabrica"), Admin, CancellationToken.None);
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
    [Fact]
    public async Task CriticalComponentState_PropagatesMostRestrictiveStateToUnit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("CFA", "CFA"), Admin, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("CHASIS", "Chasis"), Admin, CancellationToken.None);
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "CHASIS", 1, 1, true, [new AllowedComponentRequest("MONTABLE")]), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-STATE", "CFA state", "CFA", "F001", "OPERATIVO_FAENA"), Admin, CancellationToken.None);
        await fixture.Service.MountAsync("CFA-STATE", new MountOperationalUnitComponentRequest("CHF-TWCK41", "CHASIS", Motivo: "Montaje controlado"), Admin, CancellationToken.None);

        var assetService = new AssetService(fixture.Db, new PostgreSqlAuditService(fixture.Db, new AuditContextAccessor()), new AuthorizationPolicyService());
        await assetService.AddStateEventAsync("CHF-TWCK41", new CreateAssetStateEventRequest("FUERA_SERVICIO_TALLER", "Falla critica", TipoAntecedente: "OT", AntecedenteId: "OT-TEST"), Admin, CancellationToken.None);
        var unit = await fixture.Service.GetAsync("CFA-STATE", Admin, CancellationToken.None);

        Assert.NotNull(unit);
        Assert.Equal("FUERA_SERVICIO_TALLER", unit!.EstadoOperacionalCodigo);
        Assert.Equal("FUERA_SERVICIO_TALLER", unit.EstadoDerivado!.EstadoCodigo);
        Assert.Equal("CHF-TWCK41", unit.EstadoDerivado.ActivoRestrictivoCodigo);
        Assert.Equal("CHASIS", unit.EstadoDerivado.RolRestrictivoCodigo);
    }
    [Fact]
    public async Task CriticalRoles_RejectInvalidMaximumDuplicateSlotsAndCrossUnitAsset()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("CFA", "CFA"), Admin, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("CHASIS", "Chasis"), Admin, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("FABRICA", "Fabrica"), Admin, CancellationToken.None);
        var permitted = new[] { new AllowedComponentRequest("MONTABLE") };

        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "CHASIS", 1, 2, true, permitted), Admin, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "FABRICA", 1, 2, true, permitted), Admin, CancellationToken.None));
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "CHASIS", 1, 1, true, permitted), Admin, CancellationToken.None);
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "FABRICA", 1, 1, true, permitted), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-A", "CFA A", "CFA", "F001", "OPERATIVO_FAENA"), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-B", "CFA B", "CFA", "F001", "OPERATIVO_FAENA"), Admin, CancellationToken.None);

        var type = await fixture.Db.AssetTypes.SingleAsync(assetType => assetType.Code == "MONTABLE");
        var faena = await fixture.Db.Faenas.SingleAsync(site => site.Code == "F001");
        var state = await fixture.Db.AssetOperationalStates.SingleAsync(operationalState => operationalState.Code == "OPERATIVO_FAENA");
        fixture.Db.Assets.Add(new AssetEntity { Code = "CHASSIS-2", Name = "Chasis 2", AssetTypeId = type.Id, FaenaId = faena.Id, OperationalStateId = state.Id });
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.MountAsync("CFA-A", new MountOperationalUnitComponentRequest("CHF-TWCK41", "CHASIS", Motivo: "Montaje inicial"), Admin, CancellationToken.None);
        await fixture.Service.MountAsync("CFA-A", new MountOperationalUnitComponentRequest("AUGER-1000", "FABRICA", Motivo: "Montaje inicial"), Admin, CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.MountAsync("CFA-A", new MountOperationalUnitComponentRequest("QUADRA-1020", "FABRICA", Motivo: "Duplicado"), Admin, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.MountAsync("CFA-A", new MountOperationalUnitComponentRequest("CHASSIS-2", "CHASIS", Motivo: "Duplicado"), Admin, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.MountAsync("CFA-B", new MountOperationalUnitComponentRequest("AUGER-1000", "FABRICA", Motivo: "Segundo montaje"), Admin, CancellationToken.None));
    }

    [Fact]
    public async Task MountedComponent_CannotTransferAlone_AndFactoryStateRecoversUnit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("CFA", "CFA"), Admin, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("FABRICA", "Fabrica"), Admin, CancellationToken.None);
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "FABRICA", 1, 1, true, [new AllowedComponentRequest("MONTABLE")]), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-TRANSFER", "CFA transfer", "CFA", "F001", "OPERATIVO_FAENA"), Admin, CancellationToken.None);
        await fixture.Service.MountAsync("CFA-TRANSFER", new MountOperationalUnitComponentRequest("AUGER-1000", "FABRICA", Motivo: "Montaje controlado"), Admin, CancellationToken.None);

        var destination = new FaenaEntity { Code = "F002", Name = "Faena destino", IsActive = true };
        fixture.Db.AddRange(destination, new TechnicalLocationEntity { Code = "UT-F002", Name = "Ubicacion destino", Faena = destination, IsObsolete = false });
        await fixture.Db.SaveChangesAsync();
        var assetService = new AssetService(fixture.Db, new PostgreSqlAuditService(fixture.Db, new AuditContextAccessor()), new AuthorizationPolicyService());

        await Assert.ThrowsAsync<DomainException>(() => assetService.TransferAsync("AUGER-1000", new TransferAssetRequest("F002", DateTimeOffset.UtcNow.AddMinutes(1), "Traslado aislado"), Admin, CancellationToken.None));
        await assetService.AddStateEventAsync("AUGER-1000", new CreateAssetStateEventRequest("FUERA_SERVICIO_TALLER", "Falla de fabrica", TipoAntecedente: "OT", AntecedenteId: "OT-FAB"), Admin, CancellationToken.None);
        var restricted = await fixture.Service.GetAsync("CFA-TRANSFER", Admin, CancellationToken.None);
        Assert.Equal("FUERA_SERVICIO_TALLER", restricted!.EstadoOperacionalCodigo);
        Assert.Equal("FABRICA", restricted.EstadoDerivado!.RolRestrictivoCodigo);

        await assetService.AddStateEventAsync("AUGER-1000", new CreateAssetStateEventRequest("OPERATIVO_FAENA", "Reparacion terminada", TipoAntecedente: "OT", AntecedenteId: "OT-FAB"), Admin, CancellationToken.None);
        var recovered = await fixture.Service.GetAsync("CFA-TRANSFER", Admin, CancellationToken.None);
        Assert.Equal("OPERATIVO_FAENA", recovered!.EstadoOperacionalCodigo);
    }

    [Fact]
    public async Task ConcurrentMount_LeavesTheAssetInExactlyOneUnit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("CFA", "CFA"), Admin, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("FABRICA", "Fabrica"), Admin, CancellationToken.None);
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "FABRICA", 0, 1, false, [new AllowedComponentRequest("MONTABLE")]), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-CON-A", "CFA concurrente A", "CFA", "F001", "OPERATIVO_FAENA"), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-CON-B", "CFA concurrente B", "CFA", "F001", "OPERATIVO_FAENA"), Admin, CancellationToken.None);

        var connectionString = PostgreSqlWorkTestFixture.ConnectionString(fixture.AdminConnectionString, fixture.DatabaseName);
        await using var firstDb = new CmmsDbContext(new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(connectionString).Options);
        await using var secondDb = new CmmsDbContext(new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(connectionString).Options);
        var firstService = new OperationalUnitService(firstDb, new PostgreSqlAuditService(firstDb, new AuditContextAccessor()));
        var secondService = new OperationalUnitService(secondDb, new PostgreSqlAuditService(secondDb, new AuditContextAccessor()));

        static async Task<Exception?> TryMountAsync(IOperationalUnitService service, string unit)
        {
            try
            {
                await service.MountAsync(unit, new MountOperationalUnitComponentRequest("QUADRA-1020", "FABRICA", Motivo: "Montaje concurrente"), Admin, CancellationToken.None);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        var outcomes = await Task.WhenAll(TryMountAsync(firstService, "CFA-CON-A"), TryMountAsync(secondService, "CFA-CON-B"));
        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is DomainException);
        await fixture.Db.Entry(await fixture.Db.Assets.SingleAsync(asset => asset.Code == "QUADRA-1020")).ReloadAsync();
        Assert.Equal(1, await fixture.Db.OperationalUnitComponents.CountAsync(component => component.Asset.Code == "QUADRA-1020" && component.RemovedAtUtc == null));
    }
    private sealed record Fixture(string DatabaseName, string AdminConnectionString, CmmsDbContext Db, IOperationalUnitService Service) : IAsyncDisposable
    {
        public static async Task<Fixture> CreateAsync()
        {
            var name = $"cmms_test_operational_unit_{Guid.NewGuid():N}";
            var adminConnectionString = await PostgreSqlWorkTestFixture.GetAdminConnectionStringAsync();
            await PostgreSqlWorkTestFixture.CreateDatabaseAsync(name, adminConnectionString);
            var db = new CmmsDbContext(new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(PostgreSqlWorkTestFixture.ConnectionString(adminConnectionString, name)).Options);
            await db.Database.MigrateAsync();
            var faena = new FaenaEntity { Code = "F001", Name = "Faena", IsActive = true }; var type = new AssetTypeEntity { Code = "MONTABLE", Name = "Montable", IsMountable = true, IsActive = true }; var state = new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo", Severity = 0, IsActive = true }; var outOfService = new AssetOperationalStateEntity { Code = "FUERA_SERVICIO_TALLER", Name = "Fuera de servicio", Severity = 100, IsActive = true };
            db.AddRange(
                faena,
                type,
                state,
                outOfService,
                new TechnicalLocationEntity
                {
                    Code = "UT-F001",
                    Name = "UbicaciÃ³n tÃ©cnica Faena",
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
