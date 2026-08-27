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
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-1000", "CFA 1000", "CFA", "F001", "OPERATIVO"), Admin, CancellationToken.None);

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
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-2000", "CFA 2000", "CFA", "F001", "OPERATIVO"), Admin, CancellationToken.None);
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
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-STATE", "CFA state", "CFA", "F001", "OPERATIVO"), Admin, CancellationToken.None);
        await fixture.Service.MountAsync("CFA-STATE", new MountOperationalUnitComponentRequest("CHF-TWCK41", "CHASIS", Motivo: "Montaje controlado"), Admin, CancellationToken.None);

        var assetService = new AssetService(fixture.Db, new PostgreSqlAuditService(fixture.Db, new AuditContextAccessor()), new AuthorizationPolicyService());
        await Assert.ThrowsAsync<DomainException>(() => assetService.AddStateEventAsync("CHF-TWCK41", new CreateAssetStateEventRequest("CORRECTIVO", "Falla critica", TipoAntecedente: "OTHER", ReferenciaAntecedente: "OT-TEST"), Admin, CancellationToken.None));
        var unit = await fixture.Service.GetAsync("CFA-STATE", Admin, CancellationToken.None);

        Assert.NotNull(unit);
        Assert.Equal("OPERATIVO", unit!.EstadoOperacionalCodigo);
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
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-A", "CFA A", "CFA", "F001", "OPERATIVO"), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-B", "CFA B", "CFA", "F001", "OPERATIVO"), Admin, CancellationToken.None);

        var type = await fixture.Db.AssetTypes.SingleAsync(assetType => assetType.Code == "MONTABLE");
        var faena = await fixture.Db.Faenas.SingleAsync(site => site.Code == "F001");
        var state = await fixture.Db.AssetOperationalStates.SingleAsync(operationalState => operationalState.Code == "OPERATIVO");
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
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-TRANSFER", "CFA transfer", "CFA", "F001", "OPERATIVO"), Admin, CancellationToken.None);
        await fixture.Service.MountAsync("CFA-TRANSFER", new MountOperationalUnitComponentRequest("AUGER-1000", "FABRICA", Motivo: "Montaje controlado"), Admin, CancellationToken.None);

        var destination = new FaenaEntity { Code = "F002", Name = "Faena destino", IsActive = true };
        fixture.Db.AddRange(destination, new TechnicalLocationEntity { Code = "UT-F002", Name = "Ubicacion destino", Faena = destination, IsObsolete = false });
        await fixture.Db.SaveChangesAsync();
        var assetService = new AssetService(fixture.Db, new PostgreSqlAuditService(fixture.Db, new AuditContextAccessor()), new AuthorizationPolicyService());

        await Assert.ThrowsAsync<DomainException>(() => assetService.TransferAsync("AUGER-1000", new TransferAssetRequest("F002", DateTimeOffset.UtcNow.AddMinutes(1), "Traslado aislado"), Admin, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => assetService.AddStateEventAsync("AUGER-1000", new CreateAssetStateEventRequest("CORRECTIVO", "Falla de fabrica", TipoAntecedente: "OTHER", ReferenciaAntecedente: "OT-FAB"), Admin, CancellationToken.None));
        var unit = await fixture.Service.GetAsync("CFA-TRANSFER", Admin, CancellationToken.None);
        Assert.Equal("OPERATIVO", unit!.EstadoOperacionalCodigo);
    }

    [Fact]
    public async Task ConcurrentMount_LeavesTheAssetInExactlyOneUnit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("CFA", "CFA"), Admin, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("FABRICA", "Fabrica"), Admin, CancellationToken.None);
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "FABRICA", 0, 1, false, [new AllowedComponentRequest("MONTABLE")]), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-CON-A", "CFA concurrente A", "CFA", "F001", "OPERATIVO"), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-CON-B", "CFA concurrente B", "CFA", "F001", "OPERATIVO"), Admin, CancellationToken.None);

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
    [Fact]
    public async Task ListPageAsync_PaginatesAndCalculatesCompositionInBatch()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("PAGE", "Paginada"), Admin, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("CHASIS", "Chasis"), Admin, CancellationToken.None);
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("PAGE", "CHASIS", 1, 1, true), Admin, CancellationToken.None);
        var type = await fixture.Db.OperationalUnitTypes.SingleAsync(x => x.Code == "PAGE");
        var faena = await fixture.Db.Faenas.SingleAsync(x => x.Code == "F001");
        var state = await fixture.Db.AssetOperationalStates.SingleAsync(x => x.Code == "OPERATIVO");
        for (var index = 1; index <= 30; index++) fixture.Db.OperationalUnits.Add(new OperationalUnitEntity { Code = $"PAGE-{index:D3}", Name = $"Unidad paginada {index:D3}", OperationalUnitTypeId = type.Id, FaenaId = faena.Id, OperationalStateId = state.Id, BaselineOperationalStateId = state.Id });
        await fixture.Db.SaveChangesAsync();

        var first = await fixture.Service.ListPageAsync(new OperationalUnitListQuery(Texto: "PAGE", Page: 1, PageSize: 25), Admin, CancellationToken.None);
        var second = await fixture.Service.ListPageAsync(new OperationalUnitListQuery(Texto: "PAGE", Page: 2, PageSize: 25), Admin, CancellationToken.None);
        var restricted = new UserAccessContext("viewer-f002", [AuthRoles.FaenaViewer], [AuthPermissions.ViewOperationalUnits], ["F002"]);
        var inaccessible = await fixture.Service.ListPageAsync(new OperationalUnitListQuery(Texto: "PAGE", Page: 1, PageSize: 25), restricted, CancellationToken.None);

        Assert.Equal(30, first.TotalCount);
        Assert.True(first.HasNextPage);
        Assert.Equal(25, first.Items.Count);
        Assert.Equal(5, second.Items.Count);
        Assert.All(first.Items, item => { Assert.False(item.ComposicionCompleta); Assert.Contains("CHASIS", item.RolesFaltantes); });
        Assert.Empty(first.Items.Select(x => x.Codigo).Intersect(second.Items.Select(x => x.Codigo)));
        Assert.Empty(inaccessible.Items);

        var counter = new DbCommandCounter();
        var options = new DbContextOptionsBuilder<CmmsDbContext>()
            .UseNpgsql(PostgreSqlWorkTestFixture.ConnectionString(fixture.AdminConnectionString, fixture.DatabaseName))
            .AddInterceptors(counter)
            .Options;
        await using var measuredDb = new CmmsDbContext(options);
        var measuredService = new OperationalUnitService(measuredDb, new PostgreSqlAuditService(measuredDb, new AuditContextAccessor()));

        counter.Reset();
        await measuredService.ListPageAsync(new OperationalUnitListQuery(Texto: "PAGE", Page: 1, PageSize: 25), Admin, CancellationToken.None);
        var commandsFor25 = counter.Count;
        counter.Reset();
        await measuredService.ListPageAsync(new OperationalUnitListQuery(Texto: "PAGE", Page: 1, PageSize: 50), Admin, CancellationToken.None);
        var commandsFor50 = counter.Count;

        Assert.Equal(4, commandsFor25);
        Assert.Equal(commandsFor25, commandsFor50);
    }
    [Fact]
    public async Task CompleteUnit_RegistersReadingsAndStateAtomically_AndRejectsIndividualComponentOperations()
    {
        await using var fixture = await Fixture.CreateAsync();
        var operationalUser = new UserAccessContext("operator", [AuthRoles.Admin], [AuthPermissions.ViewOperationalUnits, AuthPermissions.ManageOperationalUnits, AuthPermissions.ManageOperationalUnitComposition, AuthPermissions.RegisterAssetReadings, AuthPermissions.CorrectAssetReadings, AuthPermissions.ManageAssets], ["F001"]);
        await fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("CFA", "Camión fábrica"), operationalUser, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("CHASIS", "Chasis"), operationalUser, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("FABRICA", "Fábrica"), operationalUser, CancellationToken.None);
        var permitted = new[] { new AllowedComponentRequest("MONTABLE") };
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "CHASIS", 1, 1, true, permitted), operationalUser, CancellationToken.None);
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "FABRICA", 1, 1, true, permitted), operationalUser, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-OPS", "CFA operaciones", "CFA", "F001", "OPERATIVO"), operationalUser, CancellationToken.None);
        var faena = await fixture.Db.Faenas.SingleAsync(item => item.Code == "F001");
        var assets = await fixture.Db.Assets.Where(item => item.Code == "CHF-TWCK41" || item.Code == "AUGER-1000").ToArrayAsync();
        foreach (var asset in assets)
        {
            asset.UsageMeasurementType = "HOROMETRO";
            fixture.Db.AssetPhysicalLocationPeriods.Add(new AssetPhysicalLocationPeriodEntity { AssetId = asset.Id, FaenaId = faena.Id, LocationType = "FAENA", ValidFromUtc = DateTimeOffset.UtcNow.AddDays(-1), RegisteredByUserId = "seed" });
        }
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.MountAsync("CFA-OPS", new MountOperationalUnitComponentRequest("CHF-TWCK41", "CHASIS", Motivo: "Montaje inicial"), operationalUser, CancellationToken.None);
        await fixture.Service.MountAsync("CFA-OPS", new MountOperationalUnitComponentRequest("AUGER-1000", "FABRICA", Motivo: "Montaje inicial"), operationalUser, CancellationToken.None);

        // PostgreSQL/Testcontainers regression: GET before and after each synchronous unit-reading operation must map without lazy-loading Asset.
        var beforeReading = await fixture.Service.GetAsync("CFA-OPS", operationalUser, CancellationToken.None);
        Assert.NotNull(beforeReading);
        Assert.Null(beforeReading!.UltimaLectura);

        var readAt = DateTimeOffset.UtcNow;
        var reading = await fixture.Service.AddReadingAsync("CFA-OPS", new CreateAssetReadingRequest(34000m, readAt), operationalUser, CancellationToken.None);
        Assert.Equal(2, reading.Lecturas.Count);
        Assert.All(reading.Lecturas, item => Assert.Equal(34000m, item.Valor));
        Assert.Equal(2, await fixture.Db.AssetReadings.CountAsync(item => item.OperationalUnitId != null));

var afterReading = await fixture.Service.GetAsync("CFA-OPS", operationalUser, CancellationToken.None);
        Assert.NotNull(afterReading);
        Assert.Equal(34000m, afterReading!.UltimaLectura);
        Assert.True(afterReading.PuedeCorregirLectura);

        await fixture.Service.AddReadingAsync("CFA-OPS", new CreateAssetReadingRequest(34444m, readAt.AddMinutes(1)), operationalUser, CancellationToken.None);
        await fixture.Service.AddReadingAsync("CFA-OPS", new CreateAssetReadingRequest(35000m, readAt.AddMinutes(2)), operationalUser, CancellationToken.None);
        var candidates = await fixture.Service.GetCorrectableReadingsAsync("CFA-OPS", operationalUser, CancellationToken.None);
        Assert.Equal(new decimal[] { 35000m, 34444m, 34000m }, candidates.Select(item => item.Valor).ToArray());
        var selected = Assert.Single(candidates.Where(item => item.Valor == 34444m));
        var correction = await fixture.Service.CorrectReadingAsync("CFA-OPS", new CorrectOperationalUnitReadingRequest(selected.Id, 34500m, "Ajuste validado"), operationalUser, CancellationToken.None);
        Assert.Equal(2, correction.Lecturas.Count);
        var afterCorrection = await fixture.Service.GetAsync("CFA-OPS", operationalUser, CancellationToken.None);
        Assert.NotNull(afterCorrection);
        Assert.Equal(35000m, afterCorrection!.UltimaLectura);
        var remaining = await fixture.Service.GetCorrectableReadingsAsync("CFA-OPS", operationalUser, CancellationToken.None);
        Assert.Equal(new decimal[] { 35000m, 34000m }, remaining.Select(item => item.Valor).ToArray());
        Assert.Equal(2, await fixture.Db.AssetReadings.CountAsync(item => item.IsCorrection));
        fixture.Db.AssetOperationalStates.Add(new AssetOperationalStateEntity { Code = "CON_ALERTA", Name = "Con alerta", Severity = 25, IsActive = true });
        await fixture.Db.SaveChangesAsync();
        var state = await fixture.Service.AddStateEventAsync("CFA-OPS", new CreateAssetStateEventRequest("CON_ALERTA", "Falla de unidad"), operationalUser, CancellationToken.None);
        Assert.Equal(2, state.Eventos.Count);
        Assert.Equal(2, await fixture.Db.AssetStateEvents.CountAsync(item => item.ReferenceType == "OPERATIONAL_UNIT"));
        var operationalCodes = await fixture.Db.Assets.Where(item => item.Code == "CHF-TWCK41" || item.Code == "AUGER-1000").Join(fixture.Db.AssetOperationalStates, asset => asset.OperationalStateId, state => state.Id, (asset, item) => item.Code).ToArrayAsync();
        Assert.All(operationalCodes, item => Assert.Equal("CON_ALERTA", item));

        var assetService = new AssetService(fixture.Db, new PostgreSqlAuditService(fixture.Db, new AuditContextAccessor()), new AuthorizationPolicyService());
        await Assert.ThrowsAsync<DomainException>(() => assetService.AddReadingAsync("CHF-TWCK41", new CreateAssetReadingRequest(12600m), operationalUser, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => assetService.AddStateEventAsync("CHF-TWCK41", new CreateAssetStateEventRequest("OPERATIVO", "Intento individual"), operationalUser, CancellationToken.None));
    }
    [Fact]
    public async Task GetAsync_ProjectsCurrentPhysicalLocationForSiteAndWorkshop()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = new UserAccessContext("admin", [AuthRoles.Admin], [AuthPermissions.ViewOperationalUnits, AuthPermissions.ManageOperationalUnits, AuthPermissions.ManageOperationalUnitComposition, AuthPermissions.ManageAssets], ["F001"]);
        await fixture.Service.CreateTypeAsync(new OperationalUnitTypeRequest("CFA", "Camión fábrica"), user, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("CHASIS", "Chasis"), user, CancellationToken.None);
        await fixture.Service.CreateRoleAsync(new OperationalUnitRoleRequest("FABRICA", "Fábrica"), user, CancellationToken.None);
        var permitted = new[] { new AllowedComponentRequest("MONTABLE") };
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "CHASIS", 1, 1, true, permitted), user, CancellationToken.None);
        await fixture.Service.UpsertRuleAsync(new OperationalUnitRuleRequest("CFA", "FABRICA", 1, 1, true, permitted), user, CancellationToken.None);
        await fixture.Service.CreateAsync(new OperationalUnitRequest("CFA-LUGAR", "CFA lugar", "CFA", "F001", "OPERATIVO"), user, CancellationToken.None);

        var faena = await fixture.Db.Faenas.SingleAsync(item => item.Code == "F001");
        var components = await fixture.Db.Assets.Where(item => item.Code == "CHF-TWCK41" || item.Code == "AUGER-1000").ToArrayAsync();
        var locationStart = DateTimeOffset.UtcNow.AddMinutes(-2);
        fixture.Db.AssetPhysicalLocationPeriods.AddRange(components.Select(asset => new AssetPhysicalLocationPeriodEntity
        {
            AssetId = asset.Id,
            LocationType = "FAENA",
            FaenaId = faena.Id,
            ValidFromUtc = locationStart,
            RegisteredByUserId = user.UserId,
            Reason = "Ubicación inicial de prueba"
        }));
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.MountAsync("CFA-LUGAR", new MountOperationalUnitComponentRequest("CHF-TWCK41", "CHASIS", Motivo: "Montaje inicial"), user, CancellationToken.None);
        await fixture.Service.MountAsync("CFA-LUGAR", new MountOperationalUnitComponentRequest("AUGER-1000", "FABRICA", Motivo: "Montaje inicial"), user, CancellationToken.None);

        var atSite = await fixture.Service.GetAsync("CFA-LUGAR", user, CancellationToken.None);
        Assert.NotNull(atSite);
        Assert.Equal("FAENA", atSite!.TipoUbicacionFisica);
        Assert.Equal("Faena", atSite.NombreUbicacionFisica);

        var supervisor = new AppUserEntity { Username = "supervisor-lugar", Email = "supervisor-lugar@example.test", DisplayName = "Supervisor", PasswordHash = "test-hash", IsActive = true };
        var workshop = new WorkshopEntity { Code = "TAL-LUGAR", Name = "Taller CFA", EquipmentCapacity = 2, Commune = "Antofagasta", SupervisorUser = supervisor, IsActive = true, CreatedByUserId = user.UserId };
        fixture.Db.AddRange(supervisor, workshop);
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.RegisterWorkshopEntryAsync("CFA-LUGAR", new RegisterWorkshopEntryRequest(workshop.Code, DateTimeOffset.UtcNow.AddMinutes(1), "CORRECTIVO", Motivo: "Ingreso de prueba"), user, CancellationToken.None);

        var atWorkshop = await fixture.Service.GetAsync("CFA-LUGAR", user, CancellationToken.None);
        Assert.NotNull(atWorkshop);
        Assert.Equal("TALLER", atWorkshop!.TipoUbicacionFisica);
        Assert.Equal("Taller CFA", atWorkshop.NombreUbicacionFisica);
        var activeLocations = await fixture.Db.AssetPhysicalLocationPeriods.Where(item => components.Select(component => component.Id).Contains(item.AssetId) && item.ValidToUtc == null).ToArrayAsync();
        Assert.All(activeLocations, location => Assert.Equal(workshop.Id, location.WorkshopId));
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
            var faena = new FaenaEntity { Code = "F001", Name = "Faena", IsActive = true }; var type = new AssetTypeEntity { Code = "MONTABLE", Name = "Montable", IsMountable = true, IsActive = true }; var state = new AssetOperationalStateEntity { Code = "OPERATIVO", Name = "Operativo", Severity = 0, IsActive = true }; var outOfService = new AssetOperationalStateEntity { Code = "CORRECTIVO", Name = "Fuera de servicio", Severity = 100, IsActive = true };
            db.AddRange(
                faena,
                type,
                state,
                outOfService,
                new TechnicalLocationEntity
                {
                    Code = "UT-F001",
                    Name = "Ubicación ténica Faena",
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
