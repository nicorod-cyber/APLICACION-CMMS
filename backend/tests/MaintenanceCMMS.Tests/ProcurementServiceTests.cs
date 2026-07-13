using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class ProcurementServiceTests
{
    [Fact]
    public async Task ProcurementRows_PersistInPostgreSqlAcrossContexts()
    {
        await using var database = await PostgreSqlWorkTestFixture.CreateAsync();
        await database.DbContext.SaveOperationalRowsAsync("abastecimiento_solicitudes", [new DataRow(new Dictionary<string, string?> { ["SolicitudId"] = "AB-000001", ["Estado"] = "EnviadaAbastecimiento" })], CancellationToken.None);
        await using var second = database.NewContext();
        var rows = await second.ReadOperationalRowsAsync("abastecimiento_solicitudes", CancellationToken.None);
        Assert.Single(rows);
        Assert.Equal("AB-000001", rows[0].GetValue("SolicitudId"));
    }
}
