using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class PreventiveMaintenanceServiceTests
{
    [Fact]
    public async Task PreventiveRows_PersistInPostgreSqlAcrossContexts()
    {
        await using var database = await PostgreSqlWorkTestFixture.CreateAsync();
        await database.DbContext.SaveOperationalRowsAsync("planes_preventivos", [new DataRow(new Dictionary<string, string?> { ["Codigo"] = "PM-1", ["Activo"] = "true" })], CancellationToken.None);
        await using var second = database.NewContext();
        var rows = await second.ReadOperationalRowsAsync("planes_preventivos", CancellationToken.None);
        Assert.Single(rows);
        Assert.Equal("PM-1", rows[0].GetValue("Codigo"));
    }
}
