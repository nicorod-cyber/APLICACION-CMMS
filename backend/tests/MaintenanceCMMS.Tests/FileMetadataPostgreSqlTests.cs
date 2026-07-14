using ClosedXML.Excel;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.SharePoint;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class FileMetadataPostgreSqlTests
{
    [Fact]
    public async Task SaveGetListAndDownload_UsePostgreSqlAcrossContexts()
    {
        await using var database = await PostgreSqlWorkTestFixture.CreateAsync();
        var root = Path.Combine(Path.GetTempPath(), "cmms-file-metadata-tests", Guid.NewGuid().ToString("N"));
        var storage = CreateStorage(database.DbContext, root);

        var stored = await storage.SaveDocumentAsync(new DocumentStorageSaveRequest(
            "Documents", "Activo", "ACT-1", "evidencia.txt", "text/plain", "contenido"u8.ToArray(), "tester",
            DocumentStoragePurpose.Evidence, "FAE-1", "ACT-1"), CancellationToken.None);

        await using var secondContext = database.NewContext();
        var secondStorage = CreateStorage(secondContext, root);
        var metadata = await secondStorage.GetAsync(stored.FileKey, CancellationToken.None);
        var related = await secondStorage.ListAsync(new DocumentStorageQuery(EntityType: "Activo", EntityId: "ACT-1"), CancellationToken.None);
        var download = await secondStorage.DownloadAsync(stored.FileKey, CancellationToken.None);

        Assert.NotNull(metadata);
        Assert.Equal(stored.FileKey, metadata!.FileKey);
        Assert.Single(related);
        Assert.NotNull(download);
        Assert.Equal("contenido", System.Text.Encoding.UTF8.GetString(download!.Content));
        Assert.Equal(1, await secondContext.Files.CountAsync());
        Assert.NotNull((await secondContext.Files.SingleAsync()).Checksum);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAndRetainsReferencedPhysicalContent()
    {
        await using var database = await PostgreSqlWorkTestFixture.CreateAsync();
        var root = Path.Combine(Path.GetTempPath(), "cmms-file-delete-tests", Guid.NewGuid().ToString("N"));
        var storage = CreateStorage(database.DbContext, root);
        var stored = await storage.SaveDocumentAsync(new DocumentStorageSaveRequest(
            "Documents", "Activo", "ACT-1", "referenciado.txt", "text/plain", "contenido"u8.ToArray(), "tester"), CancellationToken.None);
        var file = await database.DbContext.Files.SingleAsync();
        var documentType = new DocumentTypeEntity { Code = "TEST", Name = "Test", IsActive = true };
        var document = new DocumentEntity { Code = "DOC-FILE", Title = "Documento", DocumentType = documentType, CreatedByUserId = "tester" };
        database.DbContext.DocumentVersions.Add(new DocumentVersionEntity
        {
            Document = document,
            VersionNumber = 1,
            VersionCode = "V1",
            FileId = file.Id,
            UploadedByUserId = "tester"
        });
        await database.DbContext.SaveChangesAsync();

        var deleted = await storage.DeleteAsync(stored.FileKey, "tester", deletePhysicalContent: true, CancellationToken.None);

        Assert.True(deleted.Deleted);
        Assert.True(deleted.RetainedBecauseReferenced);
        Assert.False(deleted.PhysicalContentDeleted);
        Assert.True(File.Exists(stored.LocalPath));
        Assert.Null(await storage.GetAsync(stored.FileKey, CancellationToken.None));
    }

    [Fact]
    public async Task SaveDocumentAsync_CompensatesPhysicalUploadWhenSqlPersistenceFails()
    {
        await using var database = await PostgreSqlWorkTestFixture.CreateAsync();
        var root = Path.Combine(Path.GetTempPath(), "cmms-file-compensation-tests", Guid.NewGuid().ToString("N"));
        var storage = CreateStorage(database.DbContext, root);

        await Assert.ThrowsAnyAsync<Exception>(() => storage.SaveDocumentAsync(new DocumentStorageSaveRequest(
            "Documents", "Activo", new string('x', 241), "fallo.txt", "text/plain", "contenido"u8.ToArray(), "tester"), CancellationToken.None));

        Assert.Empty(Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : []);
        Assert.Empty(await database.DbContext.Files.ToArrayAsync());
    }

    [Fact]
    public async Task ImportSharePointFiles_IsIdempotentAndReportsInvalidReferences()
    {
        await using var database = await PostgreSqlWorkTestFixture.CreateAsync();
        var existingAsset = await database.DbContext.Assets.SingleAsync(asset => asset.Code == "ACT-1");
        database.DbContext.Assets.Add(new AssetEntity
        {
            Code = "prueba",
            Name = "Activo importado",
            FaenaId = existingAsset.FaenaId,
            FamilyId = existingAsset.FamilyId,
            AssetTypeId = existingAsset.AssetTypeId,
            OperationalStateId = existingAsset.OperationalStateId
        });
        await database.DbContext.SaveChangesAsync();
        var importer = new FileMetadataExcelImportService(database.DbContext);

        var first = await importer.ImportAsync(new FileMetadataExcelImportRequest(FindDataFile("sharepoint_files.xlsx")), CancellationToken.None);
        var second = await importer.ImportAsync(new FileMetadataExcelImportRequest(FindDataFile("sharepoint_files.xlsx")), CancellationToken.None);

        Assert.Equal(0, first.Errors);
        Assert.Equal(7, first.Inserted);
        Assert.Equal(0, second.Errors);
        Assert.Equal(7, second.Skipped);
        Assert.Equal(7, await database.DbContext.Files.CountAsync());

        var invalidWorkbook = Path.Combine(Path.GetTempPath(), $"invalid-sharepoint-files-{Guid.NewGuid():N}.xlsx");
        CreateImportWorkbook(invalidWorkbook, "MISSING-ACTIVO");
        var invalid = await importer.ImportAsync(new FileMetadataExcelImportRequest(invalidWorkbook), CancellationToken.None);

        Assert.True(invalid.Errors > 0);
        Assert.NotEmpty(invalid.ReferencesNotFound);
        Assert.Equal(7, await database.DbContext.Files.CountAsync());
    }

    private static LocalSharePointSimulationService CreateStorage(
        MaintenanceCMMS.Infrastructure.Data.PostgreSql.CmmsDbContext dbContext,
        string root)
    {
        return new LocalSharePointSimulationService(
            dbContext,
            new PostgreSqlAuditService(dbContext, new AuditContextAccessor()),
            Options.Create(new SharePointOptions { Provider = "LocalSimulation", LocalPath = root }));
    }

    private static void CreateImportWorkbook(string path, string entityId)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        var headers = new[] { "FileKey", "FileName", "ContentType", "Mode", "Purpose", "Status", "Module", "EntityType", "EntityId", "RelativePath", "SizeBytes", "CreatedAtUtc", "CreatedBy" };
        for (var index = 0; index < headers.Length; index++) sheet.Cell(1, index + 1).Value = headers[index];
        var values = new[] { "invalid/file.txt", "file.txt", "text/plain", "LocalSimulation", "Document", "Stored", "Documents", "Activo", entityId, "invalid", "1", "2026-07-10T00:00:00Z", "tester" };
        for (var index = 0; index < values.Length; index++) sheet.Cell(2, index + 1).Value = values[index];
        workbook.SaveAs(path);
    }

    private static string FindDataFile(string fileName)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "excel", fileName);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"No se encontro data/excel/{fileName}.");
    }
}
