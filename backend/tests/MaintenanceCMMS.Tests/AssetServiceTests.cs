using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Assets;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AssetServiceTests
{
    private static readonly UserAccessContext Admin = new(
        "admin", [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.ChangeAssetFaena, AuthPermissions.ViewCosts], ["F001", "F002"]);

    [Fact]
    public async Task CreateAsync_PersistsAssetAndCalculatesDynamicCompleteness()
    {
        await using var fixture = await CreateFixtureAsync();
        var asset = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-100"), Admin, CancellationToken.None);
        var persisted = await fixture.DbContext.Assets.SingleAsync(item => item.Code == asset.Resumen.Codigo);

        Assert.Matches("^ACT-[0-9]{6}$", asset.Resumen.Codigo);
        Assert.Equal("COMPLETA", asset.Resumen.CompletitudTecnica.State);
        Assert.Equal(100, asset.Resumen.CompletitudTecnica.Percentage);
        Assert.Equal(persisted.Id, (await fixture.DbContext.Assets.SingleAsync(item => item.Code == asset.Resumen.Codigo)).Id);
    }

    [Fact]
    public async Task CreateAsync_GeneratesDistinctCodes()
    {
        await using var fixture = await CreateFixtureAsync();
        var first = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-200"), Admin, CancellationToken.None);
        var second = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-201"), Admin, CancellationToken.None);
        Assert.NotEqual(first.Resumen.Codigo, second.Resumen.Codigo);
    }
    [Fact]
    public async Task AssetDetail_IncludesDescriptiveSiteZoneAndOperationalState()
    {
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateAsync(CompleteCreateRequest("DETAIL-DESCRIPTIVE"), Admin, CancellationToken.None);
        var detail = await fixture.Service.GetByIdAsync(created.Resumen.Codigo, Admin, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("F001", detail!.Resumen.FaenaCodigo);
        Assert.Equal("Faena Norte", detail.Resumen.FaenaNombre);
        Assert.Null(detail.Resumen.Zona);
        Assert.Equal("OPERATIVO", detail.Resumen.EstadoOperacionalCodigo);
        Assert.Equal("Operativo", detail.Resumen.EstadoOperacionalNombre);
    }
    [Fact]
    public async Task StateAndReadings_UseOperationalStateAndImmutableCorrection()
    {
        await using var fixture = await CreateFixtureAsync();
        var asset = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-300"), Admin, CancellationToken.None);
        await PlaceInWorkshopAsync(fixture.DbContext, asset.Resumen.Codigo);
        await fixture.Service.AddStateEventAsync(asset.Resumen.Codigo, new CreateAssetStateEventRequest("CORRECTIVO", "Ingreso a taller"), Admin, CancellationToken.None);
        var original = await fixture.Service.AddReadingAsync(asset.Resumen.Codigo, new CreateAssetReadingRequest(100m), Admin, CancellationToken.None);
        var corrected = await fixture.Service.CorrectReadingAsync(asset.Resumen.Codigo, original!.Id, new CorrectAssetReadingRequest(110m, "Correccion respaldada"), Admin, CancellationToken.None);
        var updated = await fixture.Service.GetByIdAsync(asset.Resumen.Codigo, Admin, CancellationToken.None);

        Assert.Equal("CORRECTIVO", updated!.Resumen.EstadoOperacionalCodigo);
        Assert.NotNull(corrected);
        var readings = await fixture.Service.GetReadingsAsync(asset.Resumen.Codigo, Admin, CancellationToken.None);
        Assert.Single(readings);
        Assert.Equal(110m, readings.Single().Valor);

        await ReturnToSiteAsync(fixture.DbContext, asset.Resumen.Codigo);
        await fixture.Service.AddStateEventAsync(asset.Resumen.Codigo, new CreateAssetStateEventRequest("DADO_DE_BAJA", "Baja definitiva", TipoAntecedente: "OTHER", ReferenciaAntecedente: "Acta de baja ACTA-001"), Admin, CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.AddReadingAsync(asset.Resumen.Codigo, new CreateAssetReadingRequest(120m), Admin, CancellationToken.None));
    }

    [Fact]
    public async Task Readings_AuthorizesTechnicianRegistrationButNotCorrection()
    {
        await using var fixture = await CreateFixtureAsync();
        var asset = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-401"), Admin, CancellationToken.None);
        var technician = new UserAccessContext("tech-1", [AuthRoles.Technician], [AuthPermissions.RegisterAssetReadings], ["F001", "F002"]);
        var reading = await fixture.Service.AddReadingAsync(asset.Resumen.Codigo, new CreateAssetReadingRequest(10m), technician, CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.CorrectReadingAsync(asset.Resumen.Codigo, reading.Id, new CorrectAssetReadingRequest(11m, "Sin autorizacion"), technician, CancellationToken.None));
    }
    [Fact]
    public async Task FaenaAndStateCannotBeEditedDirectly_TransferKeepsTemporalHistory()
    {
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-TRANSFER"), Admin, CancellationToken.None);
        var directEdit = new UpdateAssetRequest(
            created.Resumen.Nombre,
            created.Resumen.TipoActivoCodigo,
            created.Resumen.FamiliaEquipoCodigo,
            "F002",
            "CORRECTIVO",
            NumeroSerie: "SER-EQ-TRANSFER",
            TipoMedicionUso: "HOROMETRO");

        var exception = await Assert.ThrowsAsync<DomainException>(() => fixture.Service.UpdateAsync(created.Resumen.Codigo, directEdit, Admin, CancellationToken.None));
        Assert.Contains("traslado", exception.Message, StringComparison.OrdinalIgnoreCase);

        var stateException = await Assert.ThrowsAsync<DomainException>(() => fixture.Service.UpdateAsync(created.Resumen.Codigo, directEdit with { FaenaCodigo = "F001" }, Admin, CancellationToken.None));
        Assert.Contains("estado", stateException.Message, StringComparison.OrdinalIgnoreCase);

        var effectiveAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var transfers = await fixture.Service.TransferAsync(
            created.Resumen.Codigo,
            new TransferAssetRequest("F002", effectiveAt, "Cambio de contrato operacional"),
            Admin,
            CancellationToken.None);
        var detail = await fixture.Service.GetByIdAsync(created.Resumen.Codigo, Admin, CancellationToken.None);
        var assetId = (await fixture.DbContext.Assets.SingleAsync(asset => asset.Code == created.Resumen.Codigo)).Id;
        var periods = await fixture.DbContext.AssetLocationPeriods.Where(item => item.AssetId == assetId).OrderBy(item => item.ValidFromUtc).ToArrayAsync();

        Assert.Single(transfers);
        Assert.Equal("F001", transfers.Single().FaenaOrigenCodigo);
        Assert.Equal("F002", detail!.Resumen.FaenaCodigo);
        Assert.Equal(2, periods.Length);
        Assert.Equal(effectiveAt, periods[0].ValidToUtc);
        Assert.Null(periods[1].ValidToUtc);
        Assert.Single(detail.HistorialTraslados!);
    }
    [Fact]
    public async Task StateEvent_ValidatesSelectedWorkOrderAndSearchesByVisibleNumber()
    {
        await using var fixture = await CreateFixtureAsync();
        var asset = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-OT-1"), Admin, CancellationToken.None);
        var workOrder = await CreateWorkOrderAsync(fixture.DbContext, asset.Resumen.Codigo, "OT-000245", "Falla sistema hidraulico");
        await PlaceInWorkshopAsync(fixture.DbContext, asset.Resumen.Codigo);

        var response = await fixture.Service.AddStateEventAsync(asset.Resumen.Codigo, new CreateAssetStateEventRequest("CORRECTIVO", "Falla detectada", TipoAntecedente: "WORK_ORDER", AntecedenteId: workOrder.Id.ToString("D")), Admin, CancellationToken.None);
        var search = await fixture.Service.SearchStateEventAntecedentsAsync(asset.Resumen.Codigo, "WORK_ORDER", "000245", 1, 1, Admin, CancellationToken.None);

        Assert.Equal("WORK_ORDER", response!.TipoAntecedente);
        Assert.Equal(workOrder.Id.ToString("D"), response.AntecedenteId);
        Assert.Equal(1, search.Total);
        Assert.Single(search.Items);
        Assert.Equal("OT-000245", search.Items.Single().Codigo);
    }

    [Fact]
    public async Task StateEventSearch_RejectsUserWithoutFaenaAccess()
    {
        await using var fixture = await CreateFixtureAsync();
        var asset = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-SCOPE"), Admin, CancellationToken.None);
        var restricted = new UserAccessContext("planner-f002", [AuthRoles.Planner], [], ["F002"]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.SearchStateEventAntecedentsAsync(asset.Resumen.Codigo, "WORK_ORDER", "OT", 1, 10, restricted, CancellationToken.None));
    }
    [Fact]
    public async Task StateEvent_RejectsMismatchedAndMissingAntecedents_AndAcceptsOtherReference()
    {
        await using var fixture = await CreateFixtureAsync();
        var asset = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-OT-2"), Admin, CancellationToken.None);
        var other = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-OT-3"), Admin, CancellationToken.None);
        var foreignOrder = await CreateWorkOrderAsync(fixture.DbContext, other.Resumen.Codigo, "OT-000246", "Falla de otro activo");
        await PlaceInWorkshopAsync(fixture.DbContext, asset.Resumen.Codigo);

        var mismatch = await Assert.ThrowsAsync<DomainException>(() => fixture.Service.AddStateEventAsync(asset.Resumen.Codigo, new CreateAssetStateEventRequest("CORRECTIVO", "Prueba", TipoAntecedente: "WORK_ORDER", AntecedenteId: foreignOrder.Id.ToString("D")), Admin, CancellationToken.None));
        Assert.Contains("no corresponde al activo", mismatch.Message, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.AddStateEventAsync(asset.Resumen.Codigo, new CreateAssetStateEventRequest("CORRECTIVO", "Prueba", TipoAntecedente: "DOCUMENT", AntecedenteId: foreignOrder.Id.ToString("D")), Admin, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.AddStateEventAsync(asset.Resumen.Codigo, new CreateAssetStateEventRequest("CORRECTIVO", "Prueba", TipoAntecedente: "WORK_ORDER"), Admin, CancellationToken.None));

        var otherReference = await fixture.Service.AddStateEventAsync(asset.Resumen.Codigo, new CreateAssetStateEventRequest("CORRECTIVO", "Instruccion recibida", TipoAntecedente: "OTHER", ReferenciaAntecedente: "Instruccion verbal del supervisor"), Admin, CancellationToken.None);
        Assert.Equal("OTHER", otherReference!.TipoAntecedente);
        Assert.Null(otherReference.AntecedenteId);
        Assert.Equal("Instruccion verbal del supervisor", otherReference.ReferenciaAntecedente);
    }

    [Fact]
    public async Task ListPageAsync_PaginatesInStableOrderAndRespectsFaenaAccess()
    {
        await using var fixture = await CreateFixtureAsync();
        var type = await fixture.DbContext.AssetTypes.SingleAsync(x => x.Code == "CAMION");
        var family = await fixture.DbContext.EquipmentFamilies.SingleAsync(x => x.Code == "CAMIONES");
        var faena = await fixture.DbContext.Faenas.SingleAsync(x => x.Code == "F001");
        var state = await fixture.DbContext.AssetOperationalStates.SingleAsync(x => x.Code == "OPERATIVO");
        for (var index = 1; index <= 30; index++) fixture.DbContext.Assets.Add(new AssetEntity { Code = $"PAGE-{index:D3}", Name = $"Activo paginado {index:D3}", AssetTypeId = type.Id, FamilyId = family.Id, FaenaId = faena.Id, OperationalStateId = state.Id });
        await fixture.DbContext.SaveChangesAsync();

        var first = await fixture.Service.ListPageAsync(new AssetListQuery(Texto: "PAGE", Page: 1, PageSize: 25), Admin, CancellationToken.None);
        var second = await fixture.Service.ListPageAsync(new AssetListQuery(Texto: "PAGE", Page: 2, PageSize: 25), Admin, CancellationToken.None);
        var restricted = new UserAccessContext("viewer-f002", [AuthRoles.FaenaViewer], [], ["F002"]);
        var inaccessible = await fixture.Service.ListPageAsync(new AssetListQuery(Texto: "PAGE", Page: 1, PageSize: 25), restricted, CancellationToken.None);

        Assert.Equal(30, first.TotalCount);
        Assert.True(first.HasNextPage);
        Assert.Equal(25, first.Items.Count);
        Assert.Equal(5, second.Items.Count);
        Assert.Empty(first.Items.Select(x => x.Codigo).Intersect(second.Items.Select(x => x.Codigo)));
        Assert.Equal(first.Items.Select(x => x.Codigo).Order().ToArray(), first.Items.Select(x => x.Codigo).ToArray());
        Assert.Empty(inaccessible.Items);

        var counter = new DbCommandCounter();
        var options = new DbContextOptionsBuilder<CmmsDbContext>()
            .UseNpgsql(PostgreSqlWorkTestFixture.ConnectionString(fixture.AdminConnectionString, fixture.DatabaseName))
            .AddInterceptors(counter)
            .Options;
        await using var measuredDb = new CmmsDbContext(options);
        var measuredService = new AssetService(measuredDb, new PostgreSqlAuditService(measuredDb, new AuditContextAccessor()), new AuthorizationPolicyService());

        counter.Reset();
        await measuredService.ListPageAsync(new AssetListQuery(Texto: "PAGE", Page: 1, PageSize: 25), Admin, CancellationToken.None);
        var commandsFor25 = counter.Count;
        counter.Reset();
        await measuredService.ListPageAsync(new AssetListQuery(Texto: "PAGE", Page: 1, PageSize: 50), Admin, CancellationToken.None);
        var commandsFor50 = counter.Count;

        Assert.Equal(8, commandsFor25);
        Assert.Equal(commandsFor25, commandsFor50);
    }
    [Fact]
    public async Task EquipmentOverview_UsesUnifiedRepresentationWithoutMountedDuplicatesAndWithBoundedQueries()
    {
        await using var fixture = await CreateFixtureAsync();
        var db = fixture.DbContext;
        var assetType = await db.AssetTypes.SingleAsync(x => x.Code == "CAMION");
        var family = await db.EquipmentFamilies.SingleAsync(x => x.Code == "CAMIONES");
        var faena = await db.Faenas.SingleAsync(x => x.Code == "F001");
        var state = await db.AssetOperationalStates.SingleAsync(x => x.Code == "OPERATIVO");
        var unitType = new OperationalUnitTypeEntity { Code = "CAMION_FABRICA", Name = "Camión fábrica", IsActive = true };
        var role = new OperationalUnitComponentRoleEntity { Code = "CHASIS", Name = "Chasis", IsActive = true };
        db.AddRange(unitType, role);
        await db.SaveChangesAsync();

        var independent = new AssetEntity { Code = "OVERVIEW-INDEPENDIENTE", Name = "Activo independiente", AssetTypeId = assetType.Id, FamilyId = family.Id, FaenaId = faena.Id, OperationalStateId = state.Id };
        var mounted = new AssetEntity { Code = "OVERVIEW-MONTADO", Name = "Chasis montado", AssetTypeId = assetType.Id, FamilyId = family.Id, FaenaId = faena.Id, OperationalStateId = state.Id };
        var loose = new AssetEntity { Code = "OVERVIEW-SUELTO", Name = "Chasis suelto", AssetTypeId = assetType.Id, FamilyId = family.Id, FaenaId = faena.Id, OperationalStateId = state.Id };
        var unit = new OperationalUnitEntity { Code = "OVERVIEW-UNIDAD", Name = "Camión fábrica", OperationalUnitTypeId = unitType.Id, FaenaId = faena.Id, OperationalStateId = state.Id };
        db.AddRange(independent, mounted, loose, unit);
        for (var index = 1; index <= 30; index++) db.Assets.Add(new AssetEntity { Code = $"OVERVIEW-{index:D3}", Name = $"Activo overview {index:D3}", AssetTypeId = assetType.Id, FamilyId = family.Id, FaenaId = faena.Id, OperationalStateId = state.Id });
        await db.SaveChangesAsync();
        db.OperationalUnitComponents.AddRange(
            new OperationalUnitComponentEntity { OperationalUnitId = unit.Id, AssetId = mounted.Id, ComponentRoleId = role.Id, InstalledAtUtc = DateTimeOffset.UtcNow, InstalledByUserId = "admin" },
            new OperationalUnitComponentEntity { OperationalUnitId = unit.Id, AssetId = loose.Id, ComponentRoleId = role.Id, InstalledAtUtc = DateTimeOffset.UtcNow.AddDays(-2), RemovedAtUtc = DateTimeOffset.UtcNow.AddDays(-1), InstalledByUserId = "admin", RemovedByUserId = "admin" });
        await db.SaveChangesAsync();

        var before = await db.Assets.CountAsync();
        var first = await fixture.Service.ListEquipmentOverviewAsync(new EquipmentOverviewQuery(Search: "OVERVIEW", Page: 1, PageSize: 25), Admin, CancellationToken.None);
        var second = await fixture.Service.ListEquipmentOverviewAsync(new EquipmentOverviewQuery(Search: "OVERVIEW", Page: 2, PageSize: 25), Admin, CancellationToken.None);
        var restricted = new UserAccessContext("viewer-f002", [AuthRoles.FaenaViewer], [], ["F002"]);
        var deniedByTerritory = await fixture.Service.ListEquipmentOverviewAsync(new EquipmentOverviewQuery(Search: "OVERVIEW"), restricted, CancellationToken.None);

        Assert.Equal(33, first.TotalCount);
        Assert.Contains(first.Items, x => x.Code == independent.Code && x.RowType == "ASSET");
        Assert.Contains(first.Items.Concat(second.Items), x => x.Code == loose.Code && x.RowType == "LOOSE_COMPONENT");
        Assert.Contains(first.Items.Concat(second.Items), x => x.Code == unit.Code && x.RowType == "COMPOSITE_UNIT");
        Assert.DoesNotContain(first.Items.Concat(second.Items), x => x.Code == mounted.Code);
        Assert.Empty(first.Items.Select(x => x.RowId).Intersect(second.Items.Select(x => x.RowId)));
        Assert.Empty(deniedByTerritory.Items);
        Assert.All(first.Items.Concat(second.Items), x =>
        {
            Assert.Null(x.LastPreventiveType);
            Assert.Null(x.UsageSinceLastPreventive);
            Assert.Null(x.ApproximateNextMaintenanceDate);
        });
        Assert.Equal(before, await db.Assets.CountAsync());

        var counter = new DbCommandCounter();
        var options = new DbContextOptionsBuilder<CmmsDbContext>()
            .UseNpgsql(PostgreSqlWorkTestFixture.ConnectionString(fixture.AdminConnectionString, fixture.DatabaseName))
            .AddInterceptors(counter)
            .Options;
        await using var measuredDb = new CmmsDbContext(options);
        var measuredService = new AssetService(measuredDb, new PostgreSqlAuditService(measuredDb, new AuditContextAccessor()), new AuthorizationPolicyService());

        counter.Reset();
        await measuredService.ListEquipmentOverviewAsync(new EquipmentOverviewQuery(Search: "OVERVIEW", Page: 1, PageSize: 25), Admin, CancellationToken.None);
        var commandsFor25 = counter.Count;
        counter.Reset();
        await measuredService.ListEquipmentOverviewAsync(new EquipmentOverviewQuery(Search: "OVERVIEW", Page: 1, PageSize: 50), Admin, CancellationToken.None);
        var commandsFor50 = counter.Count;
        Assert.InRange(commandsFor25, 1, 12);
        Assert.Equal(commandsFor25, commandsFor50);
    }
    [Fact]
    public async Task EquipmentOverview_ProjectsMountedChassisBrandYearAndSummarizesUnifiedRows()
    {
        await using var fixture = await CreateFixtureAsync();
        var db = fixture.DbContext;
        var assetType = await db.AssetTypes.SingleAsync(x => x.Code == "CAMION");
        var faena = await db.Faenas.SingleAsync(x => x.Code == "F001");
        var operating = await db.AssetOperationalStates.SingleAsync(x => x.Code == "OPERATIVO");
        var corrective = await db.AssetOperationalStates.SingleAsync(x => x.Code == "CORRECTIVO");
        var unitType = new OperationalUnitTypeEntity { Code = "RESUMEN_CAMION", Name = "Camion resumen", IsActive = true };
        var chassisRole = new OperationalUnitComponentRoleEntity { Code = "CHASIS", Name = "Chasis", IsActive = true };
        var factoryRole = new OperationalUnitComponentRoleEntity { Code = "FABRICA", Name = "Fabrica", IsActive = true };
        db.AddRange(unitType, chassisRole, factoryRole);
        await db.SaveChangesAsync();

        var direct = new AssetEntity { Code = "SUMMARY-DIRECT", Name = "Directo resumen", AssetTypeId = assetType.Id, FaenaId = faena.Id, OperationalStateId = corrective.Id, Brand = "Directa", ManufacturingYear = 2020 };
        var chassis = new AssetEntity { Code = "SUMMARY-CHASIS-1", Name = "Chasis actual", AssetTypeId = assetType.Id, FaenaId = faena.Id, OperationalStateId = operating.Id, Brand = "Marca chasis", ManufacturingYear = 2021 };
        var factory = new AssetEntity { Code = "SUMMARY-FABRICA", Name = "Fabrica", AssetTypeId = assetType.Id, FaenaId = faena.Id, OperationalStateId = operating.Id, Brand = "No debe usarse", ManufacturingYear = 1999 };
        var unit = new OperationalUnitEntity { Code = "SUMMARY-UNIDAD", Name = "Unidad resumen", OperationalUnitTypeId = unitType.Id, FaenaId = faena.Id, OperationalStateId = operating.Id };
        db.AddRange(direct, chassis, factory, unit);
        await db.SaveChangesAsync();
        db.OperationalUnitComponents.AddRange(
            new OperationalUnitComponentEntity { OperationalUnitId = unit.Id, AssetId = chassis.Id, ComponentRoleId = chassisRole.Id, InstalledAtUtc = DateTimeOffset.UtcNow, InstalledByUserId = "admin" },
            new OperationalUnitComponentEntity { OperationalUnitId = unit.Id, AssetId = factory.Id, ComponentRoleId = factoryRole.Id, InstalledAtUtc = DateTimeOffset.UtcNow, InstalledByUserId = "admin" });
        await db.SaveChangesAsync();

        var page = await fixture.Service.ListEquipmentOverviewAsync(new EquipmentOverviewQuery(Search: "SUMMARY"), Admin, CancellationToken.None);
        var summary = await fixture.Service.GetEquipmentOverviewSummaryAsync(new EquipmentOverviewQuery(Search: "SUMMARY"), Admin, CancellationToken.None);
        var projectedUnit = Assert.Single(page.Items.Where(item => item.Code == unit.Code));
        Assert.Equal("Marca chasis", projectedUnit.Brand);
        Assert.Equal((short)2021, projectedUnit.ManufacturingYear);
        Assert.Equal(page.TotalCount, summary.Total);
        Assert.Equal(1, summary.NonOperational);
        Assert.Equal(0, summary.ExpiringDocuments);

        var currentChassis = await db.OperationalUnitComponents.SingleAsync(component => component.AssetId == chassis.Id);
        currentChassis.RemovedAtUtc = DateTimeOffset.UtcNow;
        var replacement = new AssetEntity { Code = "SUMMARY-CHASIS-2", Name = "Chasis reemplazo", AssetTypeId = assetType.Id, FaenaId = faena.Id, OperationalStateId = operating.Id, Brand = null, ManufacturingYear = null };
        db.Assets.Add(replacement);
        await db.SaveChangesAsync();
        db.OperationalUnitComponents.Add(new OperationalUnitComponentEntity { OperationalUnitId = unit.Id, AssetId = replacement.Id, ComponentRoleId = chassisRole.Id, InstalledAtUtc = DateTimeOffset.UtcNow, InstalledByUserId = "admin" });
        await db.SaveChangesAsync();

        projectedUnit = Assert.Single((await fixture.Service.ListEquipmentOverviewAsync(new EquipmentOverviewQuery(Search: "SUMMARY-UNIDAD"), Admin, CancellationToken.None)).Items);
        Assert.Null(projectedUnit.Brand);
        Assert.Null(projectedUnit.ManufacturingYear);
    }
    private static async Task PlaceInWorkshopAsync(CmmsDbContext db, string assetCode)
    {
        var asset = await db.Assets.SingleAsync(x => x.Code == assetCode);
        var current = await db.AssetPhysicalLocationPeriods.SingleAsync(x => x.AssetId == asset.Id && x.ValidToUtc == null);
        var workshop = new WorkshopEntity { Code = $"TAL-{Guid.NewGuid():N}".ToUpperInvariant(), Name = "Taller de prueba", EquipmentCapacity = 1, Commune = "Antofagasta", IsActive = true, CreatedByUserId = "admin" };
        var effectiveAt = current.ValidFromUtc.AddMinutes(1);
        current.ValidToUtc = effectiveAt;
        db.Workshops.Add(workshop);
        db.AssetPhysicalLocationPeriods.Add(new AssetPhysicalLocationPeriodEntity { AssetId = asset.Id, LocationType = "TALLER", Workshop = workshop, ValidFromUtc = effectiveAt, RegisteredByUserId = "admin", Reason = "Preparacion de prueba" });
        await db.SaveChangesAsync();
    }
    private static async Task ReturnToSiteAsync(CmmsDbContext db, string assetCode)
    {
        var asset = await db.Assets.SingleAsync(x => x.Code == assetCode);
        var current = await db.AssetPhysicalLocationPeriods.SingleAsync(x => x.AssetId == asset.Id && x.ValidToUtc == null);
        var effectiveAt = current.ValidFromUtc.AddMinutes(1);
        current.ValidToUtc = effectiveAt;
        db.AssetPhysicalLocationPeriods.Add(new AssetPhysicalLocationPeriodEntity { AssetId = asset.Id, LocationType = "FAENA", FaenaId = asset.FaenaId, ValidFromUtc = effectiveAt, RegisteredByUserId = "admin", Reason = "Retorno de prueba" });
        await db.SaveChangesAsync();
    }
    private static async Task<WorkOrderEntity> CreateWorkOrderAsync(CmmsDbContext db, string assetCode, string number, string description)
    {
        var asset = await db.Assets.SingleAsync(x => x.Code == assetCode);
        var faena = await db.Faenas.SingleAsync(x => x.Id == asset.FaenaId);
        var status = await db.WorkCatalogs.SingleOrDefaultAsync(x => x.Category == "WorkOrderLifecycleStatus" && x.Code == "OTCreada");
        if (status is null)
        {
            status = new WorkCatalogEntity { Category = "WorkOrderLifecycleStatus", Code = "OTCreada", Name = "OT creada", IsActive = true };
            db.WorkCatalogs.Add(status);
        }
        var maintenanceType = await db.WorkCatalogs.SingleOrDefaultAsync(x => x.Category == "MaintenanceType" && x.Code == "Correctivo");
        if (maintenanceType is null)
        {
            maintenanceType = new WorkCatalogEntity { Category = "MaintenanceType", Code = "Correctivo", Name = "Correctivo", IsActive = true };
            db.WorkCatalogs.Add(maintenanceType);
        }
        await db.SaveChangesAsync();
        var order = new WorkOrderEntity { WorkOrderNumber = number, AssetId = asset.Id, FaenaId = faena.Id, StatusId = status.Id, MaintenanceTypeId = maintenanceType.Id, Description = description, CreatedByUserId = "admin", CreatedByUserAtUtc = DateTimeOffset.UtcNow };
        db.WorkOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }
    private static CreateAssetRequest CompleteCreateRequest(string code) => new(
        "Camion tolva", "CAMION", "CAMIONES", "F001", "OPERATIVO",
        Marca: "CAT", Modelo: "777", NumeroSerie: "SER-" + code, Propiedad: "Propio", Criticidad: "ALTA",
        TipoMedicionUso: "HOROMETRO", Atributos: [new AssetAttributeValueInput("IDENTIFICADOR", ValorTexto: "ID-" + code)]);

    private static async Task<AssetFixture> CreateFixtureAsync()
    {
        var databaseName = $"cmms_test_asset_{Guid.NewGuid():N}";
        var adminConnectionString = await PostgreSqlWorkTestFixture.GetAdminConnectionStringAsync();
        await PostgreSqlWorkTestFixture.CreateDatabaseAsync(databaseName, adminConnectionString);
        var options = new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(PostgreSqlWorkTestFixture.ConnectionString(adminConnectionString, databaseName)).Options;
        var dbContext = new CmmsDbContext(options);
        await dbContext.Database.MigrateAsync();
        await SeedCatalogsAsync(dbContext);
        return new AssetFixture(databaseName, adminConnectionString, dbContext, new AssetService(dbContext, new PostgreSqlAuditService(dbContext, new AuditContextAccessor()), new AuthorizationPolicyService()));
    }

    private static async Task SeedCatalogsAsync(CmmsDbContext dbContext)
    {
        var faena = new FaenaEntity { Code = "F001", Name = "Faena Norte", IsActive = true };
        var faenaDestino = new FaenaEntity { Code = "F002", Name = "Faena Sur", IsActive = true };
        dbContext.AddRange(
            faena,
            faenaDestino,
            new TechnicalLocationEntity
            {
                Code = "UT-F001",
                Name = "Ubicacion tecnica Faena Norte",
                FaenaId = faena.Id,
                Faena = faena,
                IsObsolete = false
            },
            new TechnicalLocationEntity
            {
                Code = "UT-F002",
                Name = "Ubicacion tecnica Faena Sur",
                FaenaId = faenaDestino.Id,
                Faena = faenaDestino,
                IsObsolete = false
            });
        var type = new AssetTypeEntity { Code = "CAMION", Name = "Camion", IsActive = true };
        dbContext.AssetTypes.Add(type);
        dbContext.WorkCatalogs.AddRange(new WorkCatalogEntity { Category = "WorkNotificationCriticality", Code = "Baja", Name = "Baja", SortOrder = 1 }, new WorkCatalogEntity { Category = "WorkNotificationCriticality", Code = "Media", Name = "Media", SortOrder = 2 }, new WorkCatalogEntity { Category = "WorkNotificationCriticality", Code = "Alta", Name = "Alta", SortOrder = 3 }, new WorkCatalogEntity { Category = "WorkNotificationCriticality", Code = "Critica", Name = "Critica", SortOrder = 4 });
        dbContext.AssetOperationalStates.AddRange(
            new AssetOperationalStateEntity { Code = "OPERATIVO", Name = "Operativo", IsActive = true },
            new AssetOperationalStateEntity { Code = "CORRECTIVO", Name = "Correctivo", Severity = 100, IsActive = true });
        await dbContext.SaveChangesAsync();
        dbContext.EquipmentFamilies.Add(new EquipmentFamilyEntity { Code = "CAMIONES", Name = "Camiones", AssetTypeId = type.Id, IsActive = true });
        dbContext.AssetAttributeDefinitions.Add(new AssetAttributeDefinitionEntity { AssetTypeId = type.Id, Code = "IDENTIFICADOR", Name = "Identificador", DataType = "TEXTO", IsRequired = true, IsIdentifier = true, IsUnique = true, IsActive = true });
        await dbContext.SaveChangesAsync();
    }

    private sealed record AssetFixture(string DatabaseName, string AdminConnectionString, CmmsDbContext DbContext, IAssetService Service) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await PostgreSqlWorkTestFixture.DropDatabaseAsync(DatabaseName, AdminConnectionString);
        }
    }
}
