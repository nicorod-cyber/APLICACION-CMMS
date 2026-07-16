using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Faenas;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Faenas;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class FaenaServiceTests
{
    private static readonly UserAccessContext Admin = new("admin", [AuthRoles.Admin], [], []);

    [Fact]
    public async Task CreateAsync_PersistsFaenaAndItsOnlyTechnicalLocation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var responsible = await fixture.AddUserAsync("responsable-create");

        var created = await fixture.Service.CreateAsync(
            Request("fn-alfa", "ut-alfa", responsible.Id),
            Admin,
            CancellationToken.None);

        Assert.Equal("FN-ALFA", created.Codigo);
        Assert.Equal("Faena FN-ALFA", created.Nombre);
        Assert.Equal("Zona Norte", created.Zona);
        Assert.Equal("Cliente Principal", created.Cliente);
        Assert.Equal("CC-100", created.CentroCostes);
        Assert.Equal("Operacion", created.TipoFaena);
        Assert.Equal("Antofagasta", created.Region);
        Assert.Equal("Calama", created.Comuna);
        Assert.Equal(-22.45m, created.Latitud);
        Assert.Equal(-68.93m, created.Longitud);
        Assert.Equal(responsible.Id, created.ResponsableUsuarioId);
        Assert.Equal("Responsable responsable-create", created.ResponsableNombre);
        Assert.True(created.Activo);
        Assert.NotNull(created.UbicacionTecnica);
        Assert.Equal("UT-ALFA", created.UbicacionTecnica!.Codigo);
        Assert.Equal("Ubicacion UT-ALFA", created.UbicacionTecnica.Nombre);

        await using var read = fixture.NewContext();
        var persisted = await read.Faenas
            .Include(item => item.TechnicalLocation)
            .SingleAsync(item => item.Code == "FN-ALFA");

        Assert.Equal(responsible.Id, persisted.ResponsibleUserId);
        Assert.NotNull(persisted.TechnicalLocation);
        Assert.Equal(persisted.Id, persisted.TechnicalLocation!.FaenaId);
        Assert.Equal(1, await read.TechnicalLocations.CountAsync(item => item.FaenaId == persisted.Id));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTheExistingOneToOneTechnicalLocation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var responsible = await fixture.AddUserAsync("responsable-update");
        await fixture.Service.CreateAsync(
            Request("fn-original", "ut-original", responsible.Id),
            Admin,
            CancellationToken.None);

        var updated = await fixture.Service.UpdateAsync(
            " FN-ORIGINAL ",
            Request("fn-actualizada", "ut-actualizada", responsible.Id, active: false, obsolete: true),
            Admin,
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("FN-ACTUALIZADA", updated!.Codigo);
        Assert.False(updated.Activo);
        Assert.NotNull(updated.UbicacionTecnica);
        Assert.Equal("UT-ACTUALIZADA", updated.UbicacionTecnica!.Codigo);
        Assert.True(updated.UbicacionTecnica.Obsoleto);

        await using var read = fixture.NewContext();
        var persisted = await read.Faenas
            .Include(item => item.TechnicalLocation)
            .SingleAsync(item => item.Code == "FN-ACTUALIZADA");

        Assert.False(persisted.IsActive);
        Assert.Equal("UT-ACTUALIZADA", persisted.TechnicalLocation!.Code);
        Assert.True(persisted.TechnicalLocation.IsObsolete);
        Assert.Equal(1, await read.TechnicalLocations.CountAsync(item => item.FaenaId == persisted.Id));
        Assert.False(await read.TechnicalLocations.AnyAsync(item => item.Code == "UT-ORIGINAL"));
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidResponsibleCoordinatesAndDuplicateTechnicalLocationCode()
    {
        await using var fixture = await Fixture.CreateAsync();
        var inactive = await fixture.AddUserAsync("responsable-inactive", active: false);
        var locked = await fixture.AddUserAsync("responsable-locked", locked: true);
        var active = await fixture.AddUserAsync("responsable-active");

        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(
            Request("fn-inactive", "ut-inactive", inactive.Id),
            Admin,
            CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(
            Request("fn-locked", "ut-locked", locked.Id),
            Admin,
            CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(
            Request("fn-bad-coordinates", "ut-bad-coordinates", active.Id, latitude: 90.01m),
            Admin,
            CancellationToken.None));

        await fixture.Service.CreateAsync(
            Request("fn-source", "ut-shared", active.Id),
            Admin,
            CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(
            Request("fn-duplicate-location", "ut-shared", active.Id),
            Admin,
            CancellationToken.None));

        await using var read = fixture.NewContext();
        Assert.False(await read.Faenas.AnyAsync(item => item.Code == "FN-INACTIVE"));
        Assert.False(await read.Faenas.AnyAsync(item => item.Code == "FN-LOCKED"));
        Assert.False(await read.Faenas.AnyAsync(item => item.Code == "FN-BAD-COORDINATES"));
        Assert.False(await read.Faenas.AnyAsync(item => item.Code == "FN-DUPLICATE-LOCATION"));
        Assert.Equal(1, await read.TechnicalLocations.CountAsync(item => item.Code == "UT-SHARED"));
    }

    [Fact]
    public async Task CreateAsync_RejectsMissingResponsibleAndTechnicalLocationInputs()
    {
        await using var fixture = await Fixture.CreateAsync();
        var active = await fixture.AddUserAsync("responsable-required");

        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(
            Request("fn-missing-responsible", "ut-missing-responsible", Guid.Empty),
            Admin,
            CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(
            Request("fn-missing-location-code", " ", active.Id),
            Admin,
            CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(
            Request("fn-missing-location-name", "ut-missing-location-name", active.Id) with
            {
                UbicacionTecnicaNombre = " "
            },
            Admin,
            CancellationToken.None));

        await using var read = fixture.NewContext();
        Assert.False(await read.Faenas.AnyAsync(item =>
            item.Code == "FN-MISSING-RESPONSIBLE" ||
            item.Code == "FN-MISSING-LOCATION-CODE" ||
            item.Code == "FN-MISSING-LOCATION-NAME"));
        Assert.False(await read.TechnicalLocations.AnyAsync(item =>
            item.Code == "UT-MISSING-RESPONSIBLE" ||
            item.Code == "UT-MISSING-LOCATION-NAME"));
    }

    [Fact]
    public async Task CreateAsync_AllowsOneResponsibleForMultipleFaenas()
    {
        await using var fixture = await Fixture.CreateAsync();
        var responsible = await fixture.AddUserAsync("responsable-multiple");

        await fixture.Service.CreateAsync(
            Request("fn-responsable-a", "ut-responsable-a", responsible.Id),
            Admin,
            CancellationToken.None);
        await fixture.Service.CreateAsync(
            Request("fn-responsable-b", "ut-responsable-b", responsible.Id),
            Admin,
            CancellationToken.None);

        await using var read = fixture.NewContext();
        var faenas = await read.Faenas
            .Include(item => item.TechnicalLocation)
            .Where(item => item.ResponsibleUserId == responsible.Id)
            .OrderBy(item => item.Code)
            .ToArrayAsync();

        Assert.Equal(["FN-RESPONSABLE-A", "FN-RESPONSABLE-B"], faenas.Select(item => item.Code));
        Assert.All(faenas, item => Assert.NotNull(item.TechnicalLocation));
    }

    [Fact]
    public async Task ListAsync_AppliesExplicitZoneClientRegionAndResponsibleFilters()
    {
        await using var fixture = await Fixture.CreateAsync();
        var northResponsible = await fixture.AddUserAsync("responsable-north");
        var southResponsible = await fixture.AddUserAsync("responsable-south");

        await fixture.Service.CreateAsync(
            Request("fn-north", "ut-north", northResponsible.Id) with
            {
                Zona = "Zona Norte",
                Cliente = "Cliente Norte",
                Region = "Antofagasta"
            },
            Admin,
            CancellationToken.None);
        await fixture.Service.CreateAsync(
            Request("fn-south", "ut-south", southResponsible.Id, region: "Biobio") with
            {
                Zona = "Zona Sur",
                Cliente = "Cliente Sur"
            },
            Admin,
            CancellationToken.None);

        var byZone = await fixture.Service.ListAsync(new FaenaQuery(Zona: "sur"), Admin, CancellationToken.None);
        var byClient = await fixture.Service.ListAsync(new FaenaQuery(Cliente: "norte"), Admin, CancellationToken.None);
        var byRegion = await fixture.Service.ListAsync(new FaenaQuery(Region: "biobio"), Admin, CancellationToken.None);
        var byResponsible = await fixture.Service.ListAsync(
            new FaenaQuery(ResponsableUsuarioId: northResponsible.Id),
            Admin,
            CancellationToken.None);

        Assert.Equal("FN-SOUTH", Assert.Single(byZone).Codigo);
        Assert.Equal("FN-NORTH", Assert.Single(byClient).Codigo);
        Assert.Equal("FN-SOUTH", Assert.Single(byRegion).Codigo);
        Assert.Equal("FN-NORTH", Assert.Single(byResponsible).Codigo);
    }

    [Fact]
    public async Task ListAndGet_ApplyScopeAndSearchFilters()
    {
        await using var fixture = await Fixture.CreateAsync();
        var responsible = await fixture.AddUserAsync("responsable-scope");
        await fixture.Service.CreateAsync(
            Request("fn-norte", "ut-norte", responsible.Id, name: "Faena Norte"),
            Admin,
            CancellationToken.None);
        await fixture.Service.CreateAsync(
            Request("fn-sur", "ut-sur", responsible.Id, name: "Faena Sur", region: "Biobio", commune: "Los Angeles"),
            Admin,
            CancellationToken.None);

        var scopedUser = new UserAccessContext("planner", [AuthRoles.Planner], [], ["FN-NORTE"]);
        var listed = await fixture.Service.ListAsync(
            new FaenaQuery(Search: "norte", Region: "antofagasta"),
            scopedUser,
            CancellationToken.None);

        var only = Assert.Single(listed);
        Assert.Equal("FN-NORTE", only.Codigo);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.GetByCodeAsync(
            "FN-SUR",
            scopedUser,
            CancellationToken.None));
    }

    [Fact]
    public void Model_UsesUniqueTechnicalLocationPerFaenaAndRestrictsResponsibleDeletion()
    {
        var options = new DbContextOptionsBuilder<CmmsDbContext>()
            .UseNpgsql("Host=localhost;Database=cmms_model;Username=cmms;Password=cmms")
            .Options;
        using var db = new CmmsDbContext(options);

        var faenaType = db.Model.FindEntityType(typeof(FaenaEntity))!;
        var locationType = db.Model.FindEntityType(typeof(TechnicalLocationEntity))!;
        var locationForeignKey = Assert.Single(locationType.GetForeignKeys().Where(
            item => item.PrincipalEntityType.ClrType == typeof(FaenaEntity)));
        var responsibleForeignKey = Assert.Single(faenaType.GetForeignKeys().Where(
            item => item.PrincipalEntityType.ClrType == typeof(AppUserEntity)));

        Assert.True(locationForeignKey.IsUnique);
        Assert.Equal(nameof(TechnicalLocationEntity.FaenaId), Assert.Single(locationForeignKey.Properties).Name);
        Assert.False(responsibleForeignKey.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, responsibleForeignKey.DeleteBehavior);
        Assert.Null(db.Model.FindEntityType(typeof(AssetEntity))!.FindProperty("TechnicalLocationId"));
        Assert.Null(db.Model.FindEntityType(typeof(OperationalUnitEntity))!.FindProperty("TechnicalLocationId"));
        Assert.Null(db.Model.FindEntityType(typeof(TechnicalNodeEntity))!.FindProperty("TechnicalLocationId"));
    }

    private static UpsertFaenaRequest Request(
        string code,
        string locationCode,
        Guid responsibleUserId,
        bool active = true,
        bool obsolete = false,
        decimal? latitude = -22.45m,
        decimal? longitude = -68.93m,
        string? name = null,
        string? region = null,
        string? commune = null) => new(
        code,
        name ?? $"Faena {code.ToUpperInvariant()}",
        "Zona Norte",
        "Cliente Principal",
        "CC-100",
        "Operacion",
        region ?? "Antofagasta",
        commune ?? "Calama",
        latitude,
        longitude,
        responsibleUserId,
        active,
        locationCode,
        $"Ubicacion {locationCode.ToUpperInvariant()}",
        obsolete);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly PostgreSqlWorkTestFixture _database;

        private Fixture(PostgreSqlWorkTestFixture database)
        {
            _database = database;
            Service = new FaenaService(database.DbContext, new AuthorizationPolicyService());
        }

        public CmmsDbContext Db => _database.DbContext;
        public IFaenaService Service { get; }

        public static async Task<Fixture> CreateAsync() => new(await PostgreSqlWorkTestFixture.CreateAsync());

        public CmmsDbContext NewContext() => _database.NewContext();

        public async Task<AppUserEntity> AddUserAsync(string username, bool active = true, bool locked = false)
        {
            var user = new AppUserEntity
            {
                Username = username,
                Email = $"{username}@example.test",
                DisplayName = $"Responsable {username}",
                PasswordHash = "test-hash",
                IsActive = active,
                IsLocked = locked
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            return user;
        }

        public ValueTask DisposeAsync() => _database.DisposeAsync();
    }
}
