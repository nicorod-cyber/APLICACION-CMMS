using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
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
        "admin",
        [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.ChangeAssetFaena, AuthPermissions.ViewCosts],
        ["F001"]);

    [Fact]
    public async Task CreateAsync_PersistsAssetAndCalculatesCompleteTechnicalRecord()
    {
        await using var fixture = await CreateFixtureAsync();

        var asset = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-100"), Admin, CancellationToken.None);
        var persisted = await fixture.DbContext.Assets.SingleAsync(item => item.Code == "EQ-100", CancellationToken.None);

        Assert.Equal("EQ-100", asset.Codigo);
        Assert.Equal("Completa", asset.CompletitudFicha.State);
        Assert.Equal(100, asset.CompletitudFicha.Percentage);
        Assert.Equal(persisted.Id, (await fixture.DbContext.Assets.SingleAsync(item => item.Code == "EQ-100")).Id);
    }

    [Fact]
    public async Task CreateAsync_BlocksDuplicatedAssetCode()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-200"), Admin, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.CreateAsync(CompleteCreateRequest("eq-200"), Admin, CancellationToken.None));

        Assert.Contains("Ya existe un activo", exception.Message);
    }

    [Fact]
    public async Task AddStateEventAsync_ChangesAssetStateAndStoresEvent()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-300"), Admin, CancellationToken.None);

        var stateEvent = await fixture.Service.AddStateEventAsync(
            "EQ-300",
            new CreateAssetStateEventRequest(AssetStatus.InMaintenance, "Ingreso a taller"),
            Admin,
            CancellationToken.None);
        var updated = await fixture.Service.GetByIdAsync("EQ-300", Admin, CancellationToken.None);
        var events = await fixture.DbContext.AssetStateEvents
            .Include(item => item.NewState)
            .Where(item => item.Asset.Code == "EQ-300")
            .ToArrayAsync(CancellationToken.None);

        Assert.NotNull(stateEvent);
        Assert.Equal(AssetStatus.InMaintenance, updated!.Estado);
        Assert.Equal("FUERA_SERVICIO_TALLER", updated.EstadoOperacional);
        Assert.Contains(events, row => row.NewState.Code == "FUERA_SERVICIO_TALLER");
    }

    [Fact]
    public async Task CreateAsync_CalculatesPartialCompleteness_WhenTechnicalFieldsAreMissing()
    {
        await using var fixture = await CreateFixtureAsync();

        var asset = await fixture.Service.CreateAsync(new CreateAssetRequest(
            "EQ-400",
            "Compresor",
            "F001",
            "Equipo",
            Familia: "COMPRESOR"), Admin, CancellationToken.None);

        Assert.Equal("Parcial", asset.CompletitudFicha.State);
        Assert.True(asset.CompletitudFicha.Percentage < 100);
        Assert.Contains("Marca", asset.CompletitudFicha.MissingFields);
    }

    private static CreateAssetRequest CompleteCreateRequest(string code)
    {
        return new CreateAssetRequest(
            code,
            "Camion tolva",
            "F001",
            "Camion",
            Familia: "CAMIONES",
            Marca: "CAT",
            Modelo: "777",
            Patente: "ABCD12",
            NumeroSerie: "SER-777",
            Propiedad: "Propio",
            Criticidad: "Alta",
            EstadoDocumental: "Vigente",
            EstadoOperacional: "OPERATIVO_FAENA",
            TechnicalFields: new Dictionary<string, string?>
            {
                ["Capacidad"] = "90 t"
            },
            FichaValidada: true);
    }

    private static async Task<AssetFixture> CreateFixtureAsync()
    {
        var databaseName = $"cmms_asset_tests_{Guid.NewGuid():N}";
        var adminConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=cmms_app;Password=cmms_app_password";
        await using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = $"Host=localhost;Port=5432;Database={databaseName};Username=cmms_app;Password=cmms_app_password";
        var options = new DbContextOptionsBuilder<CmmsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var dbContext = new CmmsDbContext(options);
        await dbContext.Database.MigrateAsync();
        await SeedCatalogsAsync(dbContext);

        var auditService = new PostgreSqlAuditService(dbContext, new AuditContextAccessor());
        var service = new AssetService(dbContext, auditService, new AuthorizationPolicyService());
        return new AssetFixture(databaseName, dbContext, service);
    }

    private static async Task SeedCatalogsAsync(CmmsDbContext dbContext)
    {
        dbContext.Faenas.Add(new FaenaEntity { Code = "F001", Name = "Faena Norte", IsActive = true });
        dbContext.EquipmentFamilies.AddRange(
            new EquipmentFamilyEntity { Code = "CAMIONES", Name = "Camiones", IsActive = true },
            new EquipmentFamilyEntity { Code = "COMPRESOR", Name = "Compresor", IsActive = true });
        dbContext.AssetOperationalStates.AddRange(
            new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo en Faena", IsActive = true },
            new AssetOperationalStateEntity { Code = "ALERTA_FAENA", Name = "Con alerta en Faena", IsActive = true },
            new AssetOperationalStateEntity { Code = "FUERA_SERVICIO_FAENA", Name = "Fuera de servicio en Faena", IsActive = true },
            new AssetOperationalStateEntity { Code = "FUERA_SERVICIO_TALLER", Name = "Fuera de servicio en Taller", IsActive = true });

        await dbContext.SaveChangesAsync();
    }

    private sealed record AssetFixture(
        string DatabaseName,
        CmmsDbContext DbContext,
        IAssetService Service) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();

            await using var connection = new NpgsqlConnection("Host=localhost;Port=5432;Database=postgres;Username=cmms_app;Password=cmms_app_password");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)";
            await command.ExecuteNonQueryAsync();
        }
    }
}
