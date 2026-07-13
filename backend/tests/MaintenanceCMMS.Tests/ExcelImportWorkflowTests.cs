using ClosedXML.Excel;
using MaintenanceCMMS.Application.Imports;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Imports;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.SharePoint;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class ExcelImportWorkflowTests
{
    [Fact]
    public async Task UploadAsync_ReturnsErrors_WhenRequiredColumnsAreMissing()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.UploadAsync(new ExcelImportUploadCommand(
            "activos",
            "activos.xlsx",
            CreateWorkbook(["Codigo"], [["EQ-001"]]),
            "admin",
            SimulateOnly: false), CancellationToken.None);

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, error => error.RowNumber == 1 && error.ColumnName == "Nombre");
        Assert.Equal(ImportStatus.Validating, result.Import.Status);
    }

    [Fact]
    public async Task UploadAsync_DetectsDuplicatedNaturalKeys()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.UploadAsync(new ExcelImportUploadCommand(
            "faenas",
            "faenas.xlsx",
            CreateWorkbook(["Codigo", "Nombre", "Empresa"], [["F001", "Faena 1", "Empresa"], ["F001", "Faena duplicada", "Empresa"]]),
            "admin",
            SimulateOnly: false), CancellationToken.None);

        Assert.Contains(result.Errors, error => error.Message.Contains("Duplicated natural key", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, result.Import.Summary.DuplicateRows);
    }

    [Fact]
    public async Task ApproveAsync_AppliesRowsToOfficialMaster()
    {
        await using var fixture = await CreateFixtureAsync();
        var upload = await fixture.Service.UploadAsync(new ExcelImportUploadCommand(
            "faenas",
            "faenas.xlsx",
            CreateWorkbook(["Codigo", "Nombre", "Empresa"], [["F100", "Faena Nueva", "Empresa"]]),
            "admin",
            SimulateOnly: false), CancellationToken.None);

        var approved = await fixture.Service.ApproveAsync(upload.Import.Id, "admin", CancellationToken.None);
        var officialRows = await fixture.Provider.ReadRowsAsync("faenas", CancellationToken.None);

        Assert.NotNull(approved);
        Assert.Equal(ImportStatus.Applied, approved!.Import.Status);
        Assert.Contains(officialRows, row => row.GetValue("Codigo") == "F100");
    }

    [Fact]
    public async Task ApproveAsync_AppliesExtendedFaenaColumns()
    {
        await using var fixture = await CreateFixtureAsync();
        var upload = await fixture.Service.UploadAsync(new ExcelImportUploadCommand(
            "faenas",
            "plantilla_faenas.xlsx",
            CreateWorkbook(
                ["Codigo", "Nombre", "Empresa", "Descripcion", "Ubicación Técnica", "centro_costes", "tipo_faena", "region", "comuna", "latitud", "longitud", "responsable", "Estado"],
                [["FAE_COL", "Collahuasi", "Collahuasi", "Faena Collahuasi", "SERV-04-17", "EC06W63001", "Mina Rajo Abierto", "Tarapaca", "Pica", "-20.967828", "-68.683631", "Jhon Alvarado", "Activa"]]),
            "admin",
            SimulateOnly: false), CancellationToken.None);

        var approved = await fixture.Service.ApproveAsync(upload.Import.Id, "admin", CancellationToken.None);
        var officialRows = await fixture.Provider.ReadRowsAsync("faenas", CancellationToken.None);
        var row = Assert.Single(officialRows, item => item.GetValue("Codigo") == "FAE_COL");

        Assert.NotNull(approved);
        Assert.Equal(ImportStatus.Applied, approved!.Import.Status);
        Assert.Equal("SERV-04-17", row.GetValue("Ubicación Técnica"));
        Assert.Equal("EC06W63001", row.GetValue("centro_costes"));
        Assert.Equal("Mina Rajo Abierto", row.GetValue("tipo_faena"));
        Assert.Equal("Tarapaca", row.GetValue("region"));
        Assert.Equal("Activa", row.GetValue("Estado"));
    }

    [Fact]
    public async Task RejectAsync_ClosesImportWithoutApplyingRows()
    {
        await using var fixture = await CreateFixtureAsync();
        var upload = await fixture.Service.UploadAsync(new ExcelImportUploadCommand(
            "faenas",
            "faenas.xlsx",
            CreateWorkbook(["Codigo", "Nombre", "Empresa"], [["F200", "Faena Rechazada", "Empresa"]]),
            "admin",
            SimulateOnly: false), CancellationToken.None);

        var rejected = await fixture.Service.RejectAsync(upload.Import.Id, "admin", "No autorizado", CancellationToken.None);
        var officialRows = await fixture.Provider.ReadRowsAsync("faenas", CancellationToken.None);

        Assert.NotNull(rejected);
        Assert.Equal(ImportStatus.Rejected, rejected!.Import.Status);
        Assert.DoesNotContain(officialRows, row => row.GetValue("Codigo") == "F200");
    }

    [Fact]
    public async Task ApproveAsync_RejectsSimulationWithoutSavingOfficialData()
    {
        await using var fixture = await CreateFixtureAsync();
        var upload = await fixture.Service.UploadAsync(new ExcelImportUploadCommand(
            "faenas",
            "faenas.xlsx",
            CreateWorkbook(["Codigo", "Nombre", "Empresa"], [["F300", "Faena Simulada", "Empresa"]]),
            "admin",
            SimulateOnly: true), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.ApproveAsync(upload.Import.Id, "admin", CancellationToken.None));

        var officialRows = await fixture.Provider.ReadRowsAsync("faenas", CancellationToken.None);
        Assert.DoesNotContain(officialRows, row => row.GetValue("Codigo") == "F300");
    }

    private static async Task<ImportFixture> CreateFixtureAsync()
    {
        var excelPath = Path.Combine(Path.GetTempPath(), "maintenance-cmms-import-tests", Guid.NewGuid().ToString("N"), "excel");
        var importPath = Path.Combine(Path.GetTempPath(), "maintenance-cmms-import-tests", Guid.NewGuid().ToString("N"), "imports");
        var provider = new ExcelDataProvider(
            new ExcelSchemaRegistry(),
            Options.Create(new DataProviderSettings
            {
                Provider = "Excel",
                ExcelPath = excelPath
            }));

        await provider.InitializeAsync(CancellationToken.None);
        var auditService = new ExcelAuditService(provider, new AuditContextAccessor());
        var sharePointOptions = Options.Create(new SharePointOptions
        {
            Provider = "LocalSimulation",
            LocalPath = Path.Combine(Path.GetTempPath(), "maintenance-cmms-import-tests", Guid.NewGuid().ToString("N"), "sharepoint")
        });
        var database = await PostgreSqlWorkTestFixture.CreateAsync();
        var storageService = new LocalSharePointSimulationService(database.DbContext, auditService, sharePointOptions);
        var service = new ExcelImportWorkflowService(
            provider,
            new ExcelSchemaRegistry(),
            auditService,
            storageService,
            Options.Create(new ImportStorageOptions
            {
                StoragePath = importPath
            }));

        return new ImportFixture(database, provider, service);
    }

    private static byte[] CreateWorkbook(string[] headers, string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Data");

        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
        }

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = rows[rowIndex][columnIndex];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record ImportFixture(
        PostgreSqlWorkTestFixture Database,
        ExcelDataProvider Provider,
        IExcelImportWorkflowService Service) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
