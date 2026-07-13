using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Infrastructure.Alerts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AlertsExcelImportTests
{
    [Fact]
    public async Task ImportAsync_ImportsCurrentExcelIdempotently()
    {
        await using var database = await PostgreSqlWorkTestFixture.CreateAsync();
        var importer = new AlertsExcelImportService(database.DbContext);
        var request = new AlertsExcelImportRequest(
            FindDataFile("pdf_templates.xlsx"),
            FindDataFile("alert_rules.xlsx"),
            FindDataFile("alerts.xlsx"),
            FindDataFile("notifications.xlsx"));

        var first = await importer.ImportAsync(request, CancellationToken.None);
        var second = await importer.ImportAsync(request, CancellationToken.None);

        Assert.Equal(0, first.PdfTemplates.Errors);
        Assert.Equal(1, first.PdfTemplates.Inserted);
        Assert.Equal(14, first.AlertRules.Inserted);
        Assert.Equal(0, first.Alerts.Errors);
        Assert.Equal(0, first.Notifications.Errors);
        Assert.Equal(1, second.PdfTemplates.Skipped);
        Assert.Equal(14, second.AlertRules.Skipped);
        Assert.Equal(1, await database.DbContext.PdfTemplates.CountAsync());
        Assert.Equal(14, await database.DbContext.AlertRules.CountAsync());
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
