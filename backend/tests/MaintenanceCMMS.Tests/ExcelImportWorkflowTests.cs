using ClosedXML.Excel;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Imports;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Imports;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.SharePoint;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class ExcelImportWorkflowTests
{
    [Fact]
    public async Task UploadAsync_PersistsValidationErrorsInPostgreSql()
    {
        await using var fixture = await CreateFixtureAsync();
        var result = await fixture.Service.UploadAsync(new ExcelImportUploadCommand("faenas", "faenas.xlsx", Workbook(["Codigo"], [["F-001"]]), "admin", false), CancellationToken.None);
        var import = await fixture.Database.DbContext.Imports.Include(item => item.Errors).SingleAsync();
        Assert.Equal(ImportStatus.Validating, result.Import.Status);
        Assert.Contains(import.Errors, error => error.RowNumber == 1 && error.ColumnName == "Nombre");
    }

    [Fact]
    public async Task UploadAsync_DetectsDuplicateNaturalKeysWithoutExcelPersistence()
    {
        await using var fixture = await CreateFixtureAsync();
        var result = await fixture.Service.UploadAsync(new ExcelImportUploadCommand("faenas", "faenas.xlsx", Workbook(["Codigo", "Nombre", "Empresa"], [["F-001", "Uno", "Empresa"], ["F-001", "Dos", "Empresa"]]), "admin", false), CancellationToken.None);
        Assert.Equal(2, result.Import.Summary.DuplicateRows);
        Assert.All(result.Rows, row => Assert.Equal("Error", row.Operation));
        Assert.Equal(2, await fixture.Database.DbContext.ImportErrors.CountAsync());
    }

    [Fact]
    public async Task ApproveAsync_AppliesTypedFaenaAndRecordsRelationalTrace()
    {
        await using var fixture = await CreateFixtureAsync();
        var upload = await fixture.Service.UploadAsync(new ExcelImportUploadCommand("faenas", "faenas.xlsx", Workbook(["Codigo", "Nombre", "Empresa", "Estado"], [["F-100", "Faena nueva", "Empresa", "Activa"]]), "admin", false), CancellationToken.None);
        var approved = await fixture.Service.ApproveAsync(upload.Import.Id, "admin", CancellationToken.None);
        var faena = await fixture.Database.DbContext.Faenas.SingleAsync(item => item.Code == "F-100");
        await using var verification = fixture.Database.NewContext();
        var import = await verification.Imports.AsNoTracking().Include(item => item.Rows).Include(item => item.Events).SingleAsync(item => item.Id.ToString() == upload.Import.Id);
        Assert.NotNull(approved); Assert.Equal(ImportStatus.Applied, approved!.Import.Status); Assert.Equal("Faena nueva", faena.Name); Assert.True(faena.IsActive); Assert.Contains(import.Events, item => item.Status == (int)ImportStatus.Applied); Assert.NotNull(import.FileId);
    }

    [Fact]
    public async Task RejectAsync_ClosesImportWithoutChangingOfficialMaster()
    {
        await using var fixture = await CreateFixtureAsync();
        var upload = await fixture.Service.UploadAsync(new ExcelImportUploadCommand("faenas", "faenas.xlsx", Workbook(["Codigo", "Nombre", "Empresa"], [["F-200", "No aplicar", "Empresa"]]), "admin", false), CancellationToken.None);
        var rejected = await fixture.Service.RejectAsync(upload.Import.Id, "admin", "No autorizado", CancellationToken.None);
        Assert.Equal(ImportStatus.Rejected, rejected!.Import.Status);
        Assert.False(await fixture.Database.DbContext.Faenas.AnyAsync(item => item.Code == "F-200"));
    }

    [Fact]
    public async Task ApproveAsync_RejectsSimulationBeforeTypedWrite()
    {
        await using var fixture = await CreateFixtureAsync();
        var upload = await fixture.Service.UploadAsync(new ExcelImportUploadCommand("faenas", "faenas.xlsx", Workbook(["Codigo", "Nombre", "Empresa"], [["F-300", "Simulada", "Empresa"]]), "admin", true), CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.ApproveAsync(upload.Import.Id, "admin", CancellationToken.None));
        Assert.False(await fixture.Database.DbContext.Faenas.AnyAsync(item => item.Code == "F-300"));
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var database = await PostgreSqlWorkTestFixture.CreateAsync();
        var audit = new PostgreSqlAuditService(database.DbContext, new AuditContextAccessor());
        var storage = new LocalSharePointSimulationService(database.DbContext, audit, Options.Create(new SharePointOptions { Provider = "LocalSimulation", LocalPath = Path.Combine(Path.GetTempPath(), "cmms-import-tests", Guid.NewGuid().ToString("N")) }));
        var handlers = new IPostgreSqlImportHandler[] { new FaenaPostgreSqlImportHandler(database.DbContext), new AssetPostgreSqlImportHandler(database.DbContext), new TechnicalLocationPostgreSqlImportHandler(database.DbContext), new SparePartPostgreSqlImportHandler(database.DbContext), new WarehousePostgreSqlImportHandler(database.DbContext) };
        return new Fixture(database, new ExcelImportWorkflowService(database.DbContext, new ExcelSchemaRegistry(), new PostgreSqlImportHandlerResolver(handlers), audit, storage));
    }

    private static byte[] Workbook(string[] headers, string[][] rows)
    {
        using var workbook = new XLWorkbook(); var sheet = workbook.Worksheets.Add("Data");
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        for (var row = 0; row < rows.Length; row++) for (var column = 0; column < rows[row].Length; column++) sheet.Cell(row + 2, column + 1).Value = rows[row][column];
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    private sealed record Fixture(PostgreSqlWorkTestFixture Database, IExcelImportWorkflowService Service) : IAsyncDisposable
    { public ValueTask DisposeAsync() => Database.DisposeAsync(); }
}