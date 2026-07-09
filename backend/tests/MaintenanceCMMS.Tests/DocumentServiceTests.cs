using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Documents;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class DocumentServiceTests
{
    private static readonly UserAccessContext Admin = new(
        "admin",
        [AuthRoles.Admin],
        [
            AuthPermissions.Administration,
            AuthPermissions.ManageDocuments,
            AuthPermissions.ValidateDocuments,
            AuthPermissions.ConfigureDocumentTypes
        ],
        []);

    [Fact]
    public async Task ValidatedDocument_WithPastExpiry_IsExpired()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("REV-TEC", alertDays: 15), Admin, CancellationToken.None);

        var document = await fixture.Service.CreateAsync(
            Document("EQ-001", "REV-TEC", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), critical: true),
            Admin,
            CancellationToken.None);
        var validated = await fixture.Service.ValidateAsync(document.DocumentoId, new ValidateDocumentRequest("Ok"), Admin, CancellationToken.None);
        var expired = await fixture.Service.GetExpiredAsync("F001", Admin, CancellationToken.None);

        Assert.Equal(DocumentLifecycleStatus.Vencido, validated!.Estado);
        Assert.Contains(expired, item => item.DocumentoId == document.DocumentoId);
    }

    [Fact]
    public async Task ValidatedDocument_InsideAlertWindow_IsExpiring()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("PERMISO", alertDays: 30), Admin, CancellationToken.None);

        var document = await fixture.Service.CreateAsync(
            Document("EQ-001", "PERMISO", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))),
            Admin,
            CancellationToken.None);
        var validated = await fixture.Service.ValidateAsync(document.DocumentoId, new ValidateDocumentRequest("Ok"), Admin, CancellationToken.None);
        var expiring = await fixture.Service.GetExpiringAsync("F001", Admin, CancellationToken.None);

        Assert.Equal(DocumentLifecycleStatus.PorVencer, validated!.Estado);
        Assert.Contains(expiring, item => item.DocumentoId == document.DocumentoId);
    }

    [Fact]
    public async Task CriticalExpiredDocument_BlocksAssetAvailability()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("SEGURO", alertDays: 30, blocksAvailability: true), Admin, CancellationToken.None);

        var document = await fixture.Service.CreateAsync(
            Document("EQ-001", "SEGURO", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), blocksAvailability: true),
            Admin,
            CancellationToken.None);
        var validated = await fixture.Service.ValidateAsync(document.DocumentoId, new ValidateDocumentRequest("Ok"), Admin, CancellationToken.None);

        Assert.True(validated!.BloqueaDisponibilidadActual);
    }

    [Fact]
    public async Task ReplaceDocument_PreservesHistoricalRecord()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("CERT", alertDays: 30), Admin, CancellationToken.None);

        var original = await fixture.Service.CreateAsync(
            Document("EQ-001", "CERT", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20))),
            Admin,
            CancellationToken.None);
        await fixture.Service.ValidateAsync(original.DocumentoId, new ValidateDocumentRequest("Ok"), Admin, CancellationToken.None);

        var replacement = await fixture.Service.ReplaceAsync(
            original.DocumentoId,
            new ReplaceDocumentRequest(
                DateOnly.FromDateTime(DateTime.UtcNow),
                DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
                "sharepoint://cert-v2.pdf",
                "https://sharepoint.example/cert-v2.pdf",
                "Renovacion anual"),
            Admin,
            CancellationToken.None);
        var all = await fixture.Service.ListAsync(new DocumentQuery(IncludeHistorical: true), Admin, CancellationToken.None);

        var historical = Assert.Single(all.Where(item => item.DocumentoId == original.DocumentoId));
        Assert.NotNull(replacement);
        Assert.Equal(DocumentLifecycleStatus.Reemplazado, historical.Estado);
        Assert.True(historical.EsHistorico);
        Assert.Equal(replacement!.DocumentoId, historical.ReemplazadoPorDocumentoId);
        Assert.Equal(original.DocumentoId, replacement.ReemplazaDocumentoId);
    }

    private static CreateDocumentTypeRequest DocumentType(
        string code,
        int alertDays,
        bool blocksAvailability = false)
    {
        return new CreateDocumentTypeRequest(
            code,
            code,
            DocumentEntityType.Activo,
            Obligatorio: true,
            Critico: blocksAvailability,
            BloqueaDisponibilidad: blocksAvailability,
            PlazoAlertaDias: alertDays,
            RolesResponsables: [AuthRoles.Planner],
            RequierePdfAlerta: false,
            PlantillaHtmlCodigo: null,
            Activo: true);
    }

    private static CreateDocumentRequest Document(
        string assetCode,
        string typeCode,
        DateOnly expiresOn,
        bool critical = false,
        bool blocksAvailability = false)
    {
        return new CreateDocumentRequest(
            DocumentEntityType.Activo,
            assetCode,
            typeCode,
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            expiresOn,
            $"sharepoint://{typeCode.ToLowerInvariant()}.pdf",
            $"https://sharepoint.example/{typeCode.ToLowerInvariant()}.pdf",
            critical,
            true,
            blocksAvailability,
            "Carga inicial");
    }

    private static async Task<DocumentFixture> CreateFixtureAsync()
    {
        var excelPath = Path.Combine(Path.GetTempPath(), "maintenance-cmms-document-tests", Guid.NewGuid().ToString("N"), "excel");
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
        var authorization = new AuthorizationPolicyService();
        await provider.SaveRowsAsync("activos", [
            new DataRow(new Dictionary<string, string?>
            {
                ["Codigo"] = "EQ-001",
                ["Nombre"] = "Camion tolva",
                ["FaenaCodigo"] = "F001",
                ["TipoActivo"] = "Equipo",
                ["Familia"] = "Camiones",
                ["Estado"] = "Active",
                ["EstadoOperacional"] = "Operativo"
            })
        ], CancellationToken.None);

        var documentService = new DocumentService(provider, auditService, authorization);

        return new DocumentFixture(provider, documentService);
    }

    private sealed record DocumentFixture(
        ExcelDataProvider Provider,
        IDocumentService Service);
}
