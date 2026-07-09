using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Documents;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        ["F001"]);

    [Fact]
    public async Task CreateDocument_PersistsTypeFileVersionAndTwoAssetAssociations()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("TST-REV", alertDays: 15, blocksAvailability: true), Admin, CancellationToken.None);

        var created = await fixture.Service.CreateAsync(
            Document(["EQ-001", "EQ-002"], "TST-REV", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45))),
            Admin,
            CancellationToken.None);

        var persistedDocuments = await fixture.DbContext.Documents.CountAsync(CancellationToken.None);
        var persistedFiles = await fixture.DbContext.Files.CountAsync(CancellationToken.None);
        var activeLinks = await fixture.DbContext.DocumentAssets.CountAsync(item => item.DocumentId == Guid.Parse(created.DocumentoId) && item.IsActive, CancellationToken.None);
        var fromFirstAsset = await fixture.Service.ListAsync(new DocumentQuery(EntidadTipo: DocumentEntityType.Activo, EntidadCodigo: "EQ-001"), Admin, CancellationToken.None);
        var fromSecondAsset = await fixture.Service.ListAsync(new DocumentQuery(EntidadTipo: DocumentEntityType.Activo, EntidadCodigo: "EQ-002"), Admin, CancellationToken.None);
        var versions = await fixture.Service.ListVersionsAsync(created.DocumentoId, Admin, CancellationToken.None);

        Assert.Equal("TST-REV", created.TipoDocumento);
        Assert.Contains("EQ-001", created.EntidadCodigos!);
        Assert.Contains("EQ-002", created.EntidadCodigos!);
        Assert.Equal(1, persistedDocuments);
        Assert.Equal(1, persistedFiles);
        Assert.Equal(2, activeLinks);
        Assert.Single(fromFirstAsset, item => item.DocumentoId == created.DocumentoId);
        Assert.Single(fromSecondAsset, item => item.DocumentoId == created.DocumentoId);
        var version = Assert.Single(versions);
        Assert.True(version.Vigente);
        Assert.Equal("sharepoint://tst-rev-v1.pdf", version.ArchivoKey);
        Assert.NotNull(created.ArchivoId);
    }

    [Fact]
    public async Task ReplaceDocument_CreatesSecondVersionAndKeepsPreviousVersion()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("TST-CERT", alertDays: 30), Admin, CancellationToken.None);
        var document = await fixture.Service.CreateAsync(
            Document(["EQ-001"], "TST-CERT", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))),
            Admin,
            CancellationToken.None);

        var replaced = await fixture.Service.ReplaceAsync(
            document.DocumentoId,
            new ReplaceDocumentRequest(
                DateOnly.FromDateTime(DateTime.UtcNow),
                DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
                "sharepoint://cert-v2.pdf",
                "https://sharepoint.example/cert-v2.pdf",
                "Renovacion anual"),
            Admin,
            CancellationToken.None);
        var versions = await fixture.Service.ListVersionsAsync(document.DocumentoId, Admin, CancellationToken.None);

        Assert.NotNull(replaced);
        Assert.Equal(document.DocumentoId, replaced!.DocumentoId);
        Assert.Equal(2, versions.Count);
        Assert.Contains(versions, item => item.NumeroVersion == 1 && !item.Vigente && item.ArchivoKey == "sharepoint://tst-cert-v1.pdf");
        Assert.Contains(versions, item => item.NumeroVersion == 2 && item.Vigente && item.ArchivoKey == "sharepoint://cert-v2.pdf");
        Assert.Equal(1, await fixture.DbContext.Documents.CountAsync(CancellationToken.None));
        Assert.Equal(2, await fixture.DbContext.Files.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnassignAsset_IsLogicalAndDocumentRemainsAvailableFromOtherAsset()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("TST-PERM", alertDays: 30), Admin, CancellationToken.None);
        var document = await fixture.Service.CreateAsync(
            Document(["EQ-001"], "TST-PERM", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20))),
            Admin,
            CancellationToken.None);

        await fixture.Service.AssignAssetsAsync(
            document.DocumentoId,
            new AssignDocumentAssetsRequest(["EQ-002"], "Compartido con activo gemelo"),
            Admin,
            CancellationToken.None);
        var unassigned = await fixture.Service.UnassignAssetAsync(
            document.DocumentoId,
            "EQ-001",
            new UnassignDocumentAssetRequest("Ya no aplica"),
            Admin,
            CancellationToken.None);

        var links = await fixture.DbContext.DocumentAssets
            .Where(item => item.DocumentId == Guid.Parse(document.DocumentoId))
            .OrderBy(item => item.Asset.Code)
            .ToArrayAsync(CancellationToken.None);
        var firstAssetDocs = await fixture.Service.ListAsync(new DocumentQuery(EntidadTipo: DocumentEntityType.Activo, EntidadCodigo: "EQ-001"), Admin, CancellationToken.None);
        var secondAssetDocs = await fixture.Service.ListAsync(new DocumentQuery(EntidadTipo: DocumentEntityType.Activo, EntidadCodigo: "EQ-002"), Admin, CancellationToken.None);

        Assert.NotNull(unassigned);
        Assert.DoesNotContain("EQ-001", unassigned!.EntidadCodigos!);
        Assert.Contains("EQ-002", unassigned.EntidadCodigos!);
        Assert.Equal(2, links.Length);
        Assert.Contains(links, item => item.Asset.Code == "EQ-001" && !item.IsActive && item.UnassignedReason == "Ya no aplica");
        Assert.Contains(links, item => item.Asset.Code == "EQ-002" && item.IsActive);
        Assert.Empty(firstAssetDocs);
        Assert.Single(secondAssetDocs, item => item.DocumentoId == document.DocumentoId);
    }

    [Fact]
    public async Task AnnulDocument_DoesNotDeleteRows()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("SEGURO", alertDays: 30), Admin, CancellationToken.None);
        var document = await fixture.Service.CreateAsync(
            Document(["EQ-001"], "SEGURO", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))),
            Admin,
            CancellationToken.None);

        var annulled = await fixture.Service.AnnulAsync(document.DocumentoId, new AnnulDocumentRequest("Carga incorrecta"), Admin, CancellationToken.None);
        var stored = await fixture.DbContext.Documents.SingleAsync(item => item.Id == Guid.Parse(document.DocumentoId), CancellationToken.None);

        Assert.NotNull(annulled);
        Assert.Equal(DocumentLifecycleStatus.Anulado, annulled!.Estado);
        Assert.True(stored.IsAnnulled);
        Assert.True(stored.IsHistorical);
        Assert.Equal(1, await fixture.DbContext.Documents.CountAsync(CancellationToken.None));
        Assert.Equal(1, await fixture.DbContext.DocumentVersions.CountAsync(CancellationToken.None));
        Assert.Equal(1, await fixture.DbContext.Files.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreateDocument_RejectsMissingTypeAndDoesNotRequireExcelFiles()
    {
        await using var fixture = await CreateFixtureAsync();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.CreateAsync(
                Document(["EQ-001"], "NO-EXISTE", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))),
                Admin,
                CancellationToken.None));

        Assert.Contains("No existe el tipo documental", exception.Message);
        Assert.False(Directory.Exists(Path.Combine(AppContext.BaseDirectory, "data", "excel")));
        Assert.Empty(await fixture.DbContext.Documents.ToArrayAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ValidatedDocument_WithPastExpiry_IsExpiredAndBlocksAvailability()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("REV-EXP", alertDays: 15, blocksAvailability: true), Admin, CancellationToken.None);
        var document = await fixture.Service.CreateAsync(
            Document(["EQ-001"], "REV-EXP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), critical: true, blocksAvailability: true),
            Admin,
            CancellationToken.None);

        var validated = await fixture.Service.ValidateAsync(document.DocumentoId, new ValidateDocumentRequest("Ok"), Admin, CancellationToken.None);
        var expired = await fixture.Service.GetExpiredAsync("F001", Admin, CancellationToken.None);

        Assert.Equal(DocumentLifecycleStatus.Vencido, validated!.Estado);
        Assert.True(validated.BloqueaDisponibilidadActual);
        Assert.Contains(expired, item => item.DocumentoId == document.DocumentoId);
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
        IReadOnlyCollection<string> assetCodes,
        string typeCode,
        DateOnly expiresOn,
        bool critical = false,
        bool blocksAvailability = false)
    {
        return new CreateDocumentRequest(
            DocumentEntityType.Activo,
            assetCodes.First(),
            typeCode,
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            expiresOn,
            $"sharepoint://{typeCode.ToLowerInvariant()}-v1.pdf",
            $"https://sharepoint.example/{typeCode.ToLowerInvariant()}-v1.pdf",
            critical,
            true,
            blocksAvailability,
            "Carga inicial",
            assetCodes,
            $"{typeCode.ToLowerInvariant()}-v1.pdf",
            "application/pdf",
            1024,
            $"sha256-{typeCode.ToLowerInvariant()}-v1");
    }

    private static async Task<DocumentFixture> CreateFixtureAsync()
    {
        var databaseName = $"cmms_document_tests_{Guid.NewGuid():N}";
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
        var service = new DocumentService(dbContext, auditService, new AuthorizationPolicyService());
        return new DocumentFixture(databaseName, dbContext, service);
    }

    private static async Task SeedCatalogsAsync(CmmsDbContext dbContext)
    {
        var faena = new FaenaEntity { Code = "F001", Name = "Faena Norte", IsActive = true };
        var family = new EquipmentFamilyEntity { Code = "CAMIONES", Name = "Camiones", IsActive = true };
        var state = new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo en Faena", IsActive = true };
        dbContext.Faenas.Add(faena);
        dbContext.EquipmentFamilies.Add(family);
        dbContext.AssetOperationalStates.Add(state);
        dbContext.Assets.AddRange(
            new AssetEntity
            {
                Code = "EQ-001",
                Name = "Camion tolva 1",
                Faena = faena,
                Family = family,
                OperationalState = state,
                AssetType = "Equipo",
                RecordStatus = "vigente"
            },
            new AssetEntity
            {
                Code = "EQ-002",
                Name = "Camion tolva 2",
                Faena = faena,
                Family = family,
                OperationalState = state,
                AssetType = "Equipo",
                RecordStatus = "vigente"
            });

        await dbContext.SaveChangesAsync();
    }

    private sealed record DocumentFixture(
        string DatabaseName,
        CmmsDbContext DbContext,
        IDocumentService Service) : IAsyncDisposable
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


