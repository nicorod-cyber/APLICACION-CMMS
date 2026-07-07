using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Assets;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AssetServiceTests
{
    private static readonly UserAccessContext Admin = new(
        "admin",
        [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.ChangeAssetFaena, AuthPermissions.ViewCosts],
        []);

    [Fact]
    public async Task CreateAsync_PersistsAssetAndCalculatesCompleteTechnicalRecord()
    {
        var fixture = await CreateFixtureAsync();

        var asset = await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-100"), Admin, CancellationToken.None);
        var rows = await fixture.Provider.ReadRowsAsync("activos", CancellationToken.None);

        Assert.Equal("EQ-100", asset.Codigo);
        Assert.Equal("Completa", asset.CompletitudFicha.State);
        Assert.Equal(100, asset.CompletitudFicha.Percentage);
        Assert.Contains(rows, row => row.GetValue("Codigo") == "EQ-100");
    }

    [Fact]
    public async Task CreateAsync_BlocksDuplicatedAssetCode()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-200"), Admin, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.CreateAsync(CompleteCreateRequest("eq-200"), Admin, CancellationToken.None));

        Assert.Contains("Ya existe un activo", exception.Message);
    }

    [Fact]
    public async Task AddStateEventAsync_ChangesAssetStateAndStoresEvent()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateAsync(CompleteCreateRequest("EQ-300"), Admin, CancellationToken.None);

        var stateEvent = await fixture.Service.AddStateEventAsync(
            "EQ-300",
            new CreateAssetStateEventRequest(AssetStatus.InMaintenance, "Ingreso a taller"),
            Admin,
            CancellationToken.None);
        var updated = await fixture.Service.GetByIdAsync("EQ-300", Admin, CancellationToken.None);
        var events = await fixture.Provider.ReadRowsAsync("asset_state_events", CancellationToken.None);

        Assert.NotNull(stateEvent);
        Assert.Equal(AssetStatus.InMaintenance, updated!.Estado);
        Assert.Contains(events, row => row.GetValue("ActivoCodigo") == "EQ-300" && row.GetValue("Estado") == "InMaintenance");
    }

    [Fact]
    public async Task CreateAsync_CalculatesPartialCompleteness_WhenTechnicalFieldsAreMissing()
    {
        var fixture = await CreateFixtureAsync();

        var asset = await fixture.Service.CreateAsync(new CreateAssetRequest(
            "EQ-400",
            "Compresor",
            "F001",
            "Equipo"), Admin, CancellationToken.None);

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
            Familia: "Camiones",
            Marca: "CAT",
            Modelo: "777",
            Patente: "ABCD12",
            NumeroSerie: "SER-777",
            Propiedad: "Propio",
            Criticidad: "Alta",
            EstadoDocumental: "Vigente",
            EstadoOperacional: "Operativo",
            TechnicalFields: new Dictionary<string, string?>
            {
                ["Capacidad"] = "90 t"
            },
            FichaValidada: true);
    }

    private static async Task<AssetFixture> CreateFixtureAsync()
    {
        var excelPath = Path.Combine(Path.GetTempPath(), "maintenance-cmms-asset-tests", Guid.NewGuid().ToString("N"), "excel");
        var provider = new ExcelDataProvider(
            new ExcelSchemaRegistry(),
            Options.Create(new DataProviderSettings
            {
                Provider = "Excel",
                ExcelPath = excelPath
            }));

        await provider.InitializeAsync(CancellationToken.None);
        await provider.SaveRowsAsync("faenas", [
            new DataRow(new Dictionary<string, string?>
            {
                ["Codigo"] = "F001",
                ["Nombre"] = "Faena Norte",
                ["Empresa"] = "Empresa"
            })
        ], CancellationToken.None);

        var auditService = new ExcelAuditService(provider, new AuditContextAccessor());
        var service = new AssetService(provider, auditService, new AuthorizationPolicyService());
        return new AssetFixture(provider, service);
    }

    private sealed record AssetFixture(
        ExcelDataProvider Provider,
        IAssetService Service);
}
