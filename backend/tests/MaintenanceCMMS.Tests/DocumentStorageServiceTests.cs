using System.Text;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.SharePoint;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class DocumentStorageServiceTests
{
    [Fact]
    public async Task LocalSimulation_SavesFileAndMetadata()
    {
        await using var fixture = await CreateFixtureAsync();

        var stored = await fixture.LocalStorage.SaveDocumentAsync(new DocumentStorageSaveRequest(
            "Documents",
            "OT",
            "OT-100",
            "evidencia.txt",
            "text/plain",
            Encoding.UTF8.GetBytes("contenido"),
            "admin",
            DocumentStoragePurpose.Evidence,
            "F001",
            "EQ-001",
            "OT-100"), CancellationToken.None);

        Assert.Equal(DocumentStorageStatus.Stored, stored.Status);
        Assert.Contains("F001/EQ-001/OT-100/Evidencias", stored.FileKey);
        Assert.True(File.Exists(stored.LocalPath));

        var metadata = await fixture.LocalStorage.GetAsync(stored.FileKey, CancellationToken.None);
        Assert.NotNull(metadata);
        Assert.Equal(stored.FileKey, metadata!.FileKey);
    }

    [Fact]
    public async Task LocalSimulation_GeneratesValidFolderRoute()
    {
        await using var fixture = await CreateFixtureAsync();

        var validation = await fixture.LocalStorage.ValidatePathAsync(new DocumentStoragePathRequest(
            "Documents",
            "Activo",
            "EQ-001",
            DocumentStoragePurpose.Document,
            "F001"), CancellationToken.None);

        Assert.True(validation.IsValid);
        Assert.Equal("F001/EQ-001/Documentos", validation.RelativePath);
    }

    [Fact]
    public async Task ManualLink_SavesMetadataWithoutLocalFile()
    {
        await using var fixture = await CreateFixtureAsync();

        var stored = await fixture.ManualStorage.SaveManualLinkAsync(new ManualDocumentLinkRequest(
            "Documents",
            "Activo",
            "EQ-002",
            "permiso.pdf",
            "https://contoso.sharepoint.com/sites/cmms/permiso.pdf",
            "admin",
            DocumentStoragePurpose.Document,
            "F001",
            "EQ-002"), CancellationToken.None);

        Assert.Equal(DocumentStorageMode.ManualLink, stored.Mode);
        Assert.Equal(DocumentStorageStatus.ManualLink, stored.Status);
        Assert.Null(stored.LocalPath);
        Assert.Equal("https://contoso.sharepoint.com/sites/cmms/permiso.pdf", stored.Url);
    }

    private static async Task<StorageFixture> CreateFixtureAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "maintenance-cmms-storage-tests", Guid.NewGuid().ToString("N"));
        var database = await PostgreSqlWorkTestFixture.CreateAsync();
        var auditService = new PostgreSqlAuditService(database.DbContext, new AuditContextAccessor());
        var sharePointOptions = Options.Create(new SharePointOptions
        {
            Provider = "LocalSimulation",
            LocalPath = Path.Combine(root, "sharepoint")
        });

        return new StorageFixture(
            database,
            new LocalSharePointSimulationService(database.DbContext, auditService, sharePointOptions),
            new SharePointManualLinkService(database.DbContext, auditService, sharePointOptions));
    }

    private sealed record StorageFixture(
        PostgreSqlWorkTestFixture Database,
        LocalSharePointSimulationService LocalStorage,
        SharePointManualLinkService ManualStorage) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
