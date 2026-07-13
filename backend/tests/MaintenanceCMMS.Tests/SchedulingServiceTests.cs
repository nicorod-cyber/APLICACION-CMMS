using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class SchedulingServiceTests
{
    [Fact]
    public async Task SchedulingRows_PersistInPostgreSqlAcrossContexts()
    {
        await using var database = await PostgreSqlWorkTestFixture.CreateAsync();
        await database.DbContext.SaveOperationalRowsAsync("programacion_ot", [new DataRow(new Dictionary<string, string?> { ["ProgramacionId"] = "PRG-1", ["NumeroOT"] = "OT-1" })], CancellationToken.None);
        await using var second = database.NewContext();
        var rows = await second.ReadOperationalRowsAsync("programacion_ot", CancellationToken.None);
        Assert.Single(rows);
        Assert.Equal("OT-1", rows[0].GetValue("NumeroOT"));
    }
}
