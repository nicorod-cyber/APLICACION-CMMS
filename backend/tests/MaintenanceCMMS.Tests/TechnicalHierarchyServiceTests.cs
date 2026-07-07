using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.TechnicalHierarchy;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using MaintenanceCMMS.Infrastructure.TechnicalHierarchy;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class TechnicalHierarchyServiceTests
{
    private static readonly UserAccessContext Admin = new(
        "admin",
        [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.ManageTechnicalHierarchy],
        []);

    [Fact]
    public async Task CreateAsync_CreatesCompleteTechnicalHierarchy()
    {
        var fixture = await CreateFixtureAsync();

        await fixture.Service.CreateAsync(new CreateTechnicalNodeRequest("S-MOT", "Motor", TechnicalHierarchyLevel.Sistema), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new CreateTechnicalNodeRequest("SS-LUB", "Lubricacion", TechnicalHierarchyLevel.Subsistema, "S-MOT"), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new CreateTechnicalNodeRequest("C-BOM", "Bomba aceite", TechnicalHierarchyLevel.Componente, "SS-LUB"), Admin, CancellationToken.None);
        var subcomponent = await fixture.Service.CreateAsync(new CreateTechnicalNodeRequest("SC-SEL", "Sello mecanico", TechnicalHierarchyLevel.Subcomponente, "C-BOM"), Admin, CancellationToken.None);

        var nodes = await fixture.Service.ListAsync(new TechnicalHierarchyQuery(), Admin, CancellationToken.None);

        Assert.Equal(4, nodes.Count);
        Assert.Equal("Motor / Lubricacion / Bomba aceite / Sello mecanico", subcomponent.Ruta);
    }

    [Fact]
    public async Task MarkObsoleteAsync_DoesNotPhysicallyDeleteUsedNode()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateAsync(new CreateTechnicalNodeRequest("S-FREN", "Frenos", TechnicalHierarchyLevel.Sistema), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new CreateTechnicalNodeRequest("SS-HID", "Hidraulico", TechnicalHierarchyLevel.Subsistema, "S-FREN"), Admin, CancellationToken.None);

        var result = await fixture.Service.MarkObsoleteAsync(
            "S-FREN",
            new MarkTechnicalNodeObsoleteRequest("Duplicado historico"),
            Admin,
            CancellationToken.None);
        var rows = await fixture.Provider.ReadRowsAsync("sistemas_componentes", CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.Obsoleto);
        Assert.Contains(rows, row => row.GetValue("Codigo") == "S-FREN" && row.GetValue("Obsoleto") == "true");
        Assert.Contains(rows, row => row.GetValue("Codigo") == "SS-HID" && row.GetValue("CodigoPadre") == "S-FREN");
    }

    [Fact]
    public async Task DetectSimilarAsync_FindsNormalizedAndTypoDuplicates()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateAsync(new CreateTechnicalNodeRequest("S-MP1", "Motor Principal", TechnicalHierarchyLevel.Sistema), Admin, CancellationToken.None);
        await fixture.Service.CreateAsync(new CreateTechnicalNodeRequest("S-MP2", "Motor Prinicpal", TechnicalHierarchyLevel.Sistema), Admin, CancellationToken.None);

        var duplicates = await fixture.Service.DetectSimilarAsync(new TechnicalHierarchyQuery(), Admin, CancellationToken.None);

        Assert.Contains(duplicates, item =>
            (item.Node.Codigo == "S-MP1" && item.Candidate.Codigo == "S-MP2") ||
            (item.Node.Codigo == "S-MP2" && item.Candidate.Codigo == "S-MP1"));
    }

    private static async Task<TechnicalHierarchyFixture> CreateFixtureAsync()
    {
        var excelPath = Path.Combine(Path.GetTempPath(), "maintenance-cmms-hierarchy-tests", Guid.NewGuid().ToString("N"), "excel");
        var provider = new ExcelDataProvider(
            new ExcelSchemaRegistry(),
            Options.Create(new DataProviderSettings
            {
                Provider = "Excel",
                ExcelPath = excelPath
            }));

        await provider.InitializeAsync(CancellationToken.None);
        var auditService = new ExcelAuditService(provider, new AuditContextAccessor());
        var service = new TechnicalHierarchyService(provider, auditService, new AuthorizationPolicyService());

        return new TechnicalHierarchyFixture(provider, service);
    }

    private sealed record TechnicalHierarchyFixture(
        ExcelDataProvider Provider,
        ITechnicalHierarchyService Service);
}
