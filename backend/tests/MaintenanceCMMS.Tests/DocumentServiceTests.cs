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
    private static readonly UserAccessContext Planner = new(
        "planner", [AuthRoles.Planner], [AuthPermissions.ManageDocuments, AuthPermissions.ReviewDocuments, AuthPermissions.ValidateDocuments, AuthPermissions.RejectDocuments], ["F001"]);

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

        var validated = await fixture.Service.ValidateAsync(document.DocumentoId, new ValidateDocumentRequest("Ok"), Planner, CancellationToken.None);
        var expired = await fixture.Service.GetExpiredAsync("F001", Admin, CancellationToken.None);

        Assert.Equal(DocumentLifecycleStatus.Vencido, validated!.Estado);
        Assert.True(validated.BloqueaDisponibilidadActual);
        Assert.Contains(expired, item => item.DocumentoId == document.DocumentoId);
    }

    [Fact]
    public async Task SupervisorCannotValidateOrReject_EvenWithDocumentPermissions()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("TST-ROLE", alertDays: 15), Admin, CancellationToken.None);
        var created = await fixture.Service.CreateAsync(
            Document(["EQ-001"], "TST-ROLE", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20))),
            Admin,
            CancellationToken.None);
        var supervisor = new UserAccessContext(
            "supervisor",
            [AuthRoles.MaintenanceSupervisor],
            [AuthPermissions.ManageDocuments, AuthPermissions.ValidateDocuments, AuthPermissions.RejectDocuments],
            ["F001"]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.ValidateAsync(created.DocumentoId, new ValidateDocumentRequest("No corresponde"), supervisor, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.RejectAsync(created.DocumentoId, new RejectDocumentRequest("No corresponde"), supervisor, CancellationToken.None));
    }

    [Fact]
    public async Task RejectionCorrectionCycle_PreservesCompleteSnapshotsAcrossVersions()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateTypeAsync(DocumentType("TST-CYCLE", alertDays: 30, blocksAvailability: true), Admin, CancellationToken.None);
        var firstExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var created = await fixture.Service.CreateAsync(Document(["EQ-001"], "TST-CYCLE", firstExpiry, critical: true, blocksAvailability: true), Admin, CancellationToken.None);

        await fixture.Service.RejectAsync(created.DocumentoId, new RejectDocumentRequest("Falta firma responsable"), Planner, CancellationToken.None);
        var secondExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        var corrected = await fixture.Service.ReplaceAsync(
            created.DocumentoId,
            new ReplaceDocumentRequest(DateOnly.FromDateTime(DateTime.UtcNow), secondExpiry, "sharepoint://cycle-v2.pdf", "https://sharepoint.example/cycle-v2.pdf", "Correccion firmada"),
            Admin,
            CancellationToken.None);
        var versions = (await fixture.Service.ListVersionsAsync(created.DocumentoId, Admin, CancellationToken.None)).OrderBy(item => item.NumeroVersion).ToArray();

        Assert.NotNull(corrected);
        Assert.Null(corrected!.RechazadoPor);
        Assert.Null(corrected.MotivoRechazo);
        Assert.Equal(DocumentLifecycleStatus.PendienteValidacion, corrected.Estado);
        Assert.Equal(2, versions.Length);
        Assert.Equal(firstExpiry, versions[0].FechaVencimiento);
        Assert.Equal(DocumentLifecycleStatus.Rechazado.ToString(), versions[0].EstadoValidacion);
        Assert.Equal("Falta firma responsable", versions[0].MotivoRechazo);
        Assert.Equal("CORREGIDO_NUEVA_VERSION", versions[0].EstadoCorreccion);
        Assert.Equal(secondExpiry, versions[1].FechaVencimiento);
        Assert.Equal(versions[0].VersionId, versions[1].ReemplazaVersionId);
        Assert.Equal(versions[0].CicloCorreccionId, versions[1].CicloCorreccionId);
        Assert.Equal("admin", versions[1].ResponsableCorreccion);
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
        var databaseName = $"cmms_test_document_{Guid.NewGuid():N}";
        var adminConnectionString = await PostgreSqlWorkTestFixture.GetAdminConnectionStringAsync();
        await PostgreSqlWorkTestFixture.CreateDatabaseAsync(databaseName, adminConnectionString);
        var connectionString = PostgreSqlWorkTestFixture.ConnectionString(adminConnectionString, databaseName);
        var options = new DbContextOptionsBuilder<CmmsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var dbContext = new CmmsDbContext(options);
        await dbContext.Database.MigrateAsync();
        await SeedCatalogsAsync(dbContext);

        var auditService = new PostgreSqlAuditService(dbContext, new AuditContextAccessor());
        var service = new DocumentService(dbContext, auditService, new AuthorizationPolicyService());
        return new DocumentFixture(databaseName, adminConnectionString, dbContext, service);
    }

    private static async Task SeedCatalogsAsync(CmmsDbContext dbContext)
    {
        var faena = new FaenaEntity { Code = "F001", Name = "Faena Norte", IsActive = true };
        var type = new AssetTypeEntity { Code = "CAMION", Name = "Camiï¿½n", IsActive = true };
        var family = new EquipmentFamilyEntity { Code = "CAMIONES", Name = "Camiones", AssetTypeId = type.Id, IsActive = true };
        var state = new AssetOperationalStateEntity { Code = "OPERATIVO_FAENA", Name = "Operativo en Faena", IsActive = true };
        dbContext.Faenas.Add(faena);
        dbContext.AssetTypes.Add(type);
        dbContext.EquipmentFamilies.Add(family);
        dbContext.AssetOperationalStates.Add(state);
        dbContext.Assets.AddRange(
            new AssetEntity
            {
                Code = "EQ-001",
                Name = "Camion tolva 1",
                Faena = faena,
                Family = family,
                OperationalState = state, AssetTypeId = type.Id
            },
            new AssetEntity
            {
                Code = "EQ-002",
                Name = "Camion tolva 2",
                Faena = faena,
                Family = family,
                OperationalState = state, AssetTypeId = type.Id
            });

        await dbContext.SaveChangesAsync();
    }

    private sealed record DocumentFixture(
        string DatabaseName,
        string AdminConnectionString,
        CmmsDbContext DbContext,
        IDocumentService Service) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();

            await PostgreSqlWorkTestFixture.DropDatabaseAsync(DatabaseName, AdminConnectionString);
        }
    }
}
