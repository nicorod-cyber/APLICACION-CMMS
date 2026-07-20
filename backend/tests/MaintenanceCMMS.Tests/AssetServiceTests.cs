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
        [AuthPermissions.Administration, AuthPermissions.ChangeAssetFaena, AuthPermissions.ViewCosts], ["F001"]);

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
    public async Task StateAndReadings_UseOperationalStateAndImmutableCorrection()
    {
        await using var fixture = await CreateFixtureAsync();
        var asset = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-300"), Admin, CancellationToken.None);
        await fixture.Service.AddStateEventAsync(asset.Resumen.Codigo, new CreateAssetStateEventRequest("FUERA_SERVICIO_TALLER", "Ingreso a taller"), Admin, CancellationToken.None);
        var original = await fixture.Service.AddReadingAsync(asset.Resumen.Codigo, new CreateAssetReadingRequest(100m), Admin, CancellationToken.None);
        var corrected = await fixture.Service.CorrectReadingAsync(asset.Resumen.Codigo, original!.Id, new CorrectAssetReadingRequest(110m, "Correccion respaldada"), Admin, CancellationToken.None);
        var updated = await fixture.Service.GetByIdAsync(asset.Resumen.Codigo, Admin, CancellationToken.None);

        Assert.Equal("FUERA_SERVICIO_TALLER", updated!.Resumen.EstadoOperacionalCodigo);
        Assert.NotNull(corrected);
        var readings = await fixture.Service.GetReadingsAsync(asset.Resumen.Codigo, Admin, CancellationToken.None);
        Assert.Single(readings);
        Assert.Equal(110m, readings.Single().Valor);
    }

    [Fact]
    public async Task Readings_AuthorizesTechnicianRegistrationButNotCorrection()
    {
        await using var fixture = await CreateFixtureAsync();
        var asset = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-401"), Admin, CancellationToken.None);
        var technician = new UserAccessContext("tech-1", [AuthRoles.Technician], [AuthPermissions.RegisterAssetReadings], ["F001"]);
        var reading = await fixture.Service.AddReadingAsync(asset.Resumen.Codigo, new CreateAssetReadingRequest(10m), technician, CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.CorrectReadingAsync(asset.Resumen.Codigo, reading.Id, new CorrectAssetReadingRequest(11m, "Sin autorizacion"), technician, CancellationToken.None));
    }
    private static CreateAssetRequest CompleteCreateRequest(string code) => new(
        "Camion tolva", "CAMION", "CAMIONES", "F001", "OPERATIVO_FAENA",
        Marca: "CAT", Modelo: "777", NumeroSerie: "SER-777", Propiedad: "Propio", Criticidad: "ALTA",
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
        dbContext.AddRange(
            faena,
            new TechnicalLocationEntity
            {
                Code = "UT-F001",
                Name = "Ubicacion tecnica Faena Norte",
                FaenaId = faena.Id,
                Faena = faena,
                IsObsolete = false
            });
        var type = new AssetTypeEntity { Code = "CAMION", Name = "Camion", IsActive = true };
        dbContext.AssetTypes.Add(type);
        dbContext.WorkCatalogs.AddRange(new WorkCatalogEntity { Category = "WorkNotificationCriticality", Code = "Baja", Name = "Baja", SortOrder = 1 }, new WorkCatalogEntity { Category = "WorkNotificationCriticality", Code = "Media", Name = "Media", SortOrder = 2 }, new WorkCatalogEntity { Category = "WorkNotificationCriticality", Code = "Alta", Name = "Alta", SortOrder = 3 }, new WorkCatalogEntity { Category = "WorkNotificationCriticality", Code = "Critica", Name = "Critica", SortOrder = 4 });
        dbContext.AssetOperationalStates.AddRange(
            new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo en Faena", IsActive = true },
            new AssetOperationalStateEntity { Code = "FUERA_SERVICIO_TALLER", Name = "Fuera de servicio en Taller", IsActive = true });
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
