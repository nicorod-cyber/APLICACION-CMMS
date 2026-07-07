using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class ExcelDataProviderTests
{
    [Fact]
    public async Task InitializeAsync_CreatesBaseExcelFiles()
    {
        var tempPath = CreateTempPath();
        var provider = CreateProvider(tempPath);

        await provider.InitializeAsync(CancellationToken.None);
        var health = await provider.CheckHealthAsync(CancellationToken.None);

        Assert.True(health.IsHealthy);
        Assert.Equal(new ExcelSchemaRegistry().GetAll().Count, health.Files.Count);
        Assert.Contains(health.Files, file => file.SchemaName == "sharepoint_files");
        Assert.All(health.Files, file =>
        {
            Assert.True(file.Exists);
            Assert.True(file.HasDataSheet);
            Assert.Empty(file.MissingColumns);
        });
    }

    [Fact]
    public async Task SaveRowsAsync_RejectsDuplicatedNaturalKeys()
    {
        var tempPath = CreateTempPath();
        var provider = CreateProvider(tempPath);
        await provider.InitializeAsync(CancellationToken.None);

        var rows = new[]
        {
            new DataRow(new Dictionary<string, string?>
            {
                ["Codigo"] = "EQ-001",
                ["Nombre"] = "Activo 1",
                ["FaenaCodigo"] = "F001",
                ["TipoActivo"] = "Camion",
                ["Estado"] = "Active"
            }),
            new DataRow(new Dictionary<string, string?>
            {
                ["Codigo"] = "EQ-001",
                ["Nombre"] = "Activo duplicado",
                ["FaenaCodigo"] = "F001",
                ["TipoActivo"] = "Camion",
                ["Estado"] = "Active"
            })
        };

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            provider.SaveRowsAsync("activos", rows, CancellationToken.None));

        Assert.Contains("Duplicated natural key", exception.Message);
    }

    [Fact]
    public async Task ImportService_ValidatesRequiredColumnsAndTypes()
    {
        var tempPath = CreateTempPath();
        var provider = CreateProvider(tempPath);
        var registry = new ExcelSchemaRegistry();
        var importService = new ImportService(registry, provider);

        var rows = new[]
        {
            new DataRow(new Dictionary<string, string?>
            {
                ["BodegaCodigo"] = "B01",
                ["RepuestoCodigo"] = "RP01",
                ["StockFisico"] = "no-numero",
                ["StockReservado"] = ""
            })
        };

        var result = await importService.ValidateAsync("stock_bodegas", rows, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ColumnName == "StockFisico");
        Assert.Contains(result.Errors, error => error.ColumnName == "StockReservado");
    }

    private static ExcelDataProvider CreateProvider(string path)
    {
        return new ExcelDataProvider(
            new ExcelSchemaRegistry(),
            Options.Create(new DataProviderSettings
            {
                Provider = "Excel",
                ExcelPath = path
            }));
    }

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), "maintenance-cmms-tests", Guid.NewGuid().ToString("N"));
    }
}
