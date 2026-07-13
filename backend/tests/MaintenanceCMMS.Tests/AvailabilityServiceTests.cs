using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AvailabilityServiceTests
{
    [Fact]
    public async Task AvailabilityRows_PersistInPostgreSqlAcrossContexts()
    {
        await using var database = await PostgreSqlWorkTestFixture.CreateAsync();
        await database.DbContext.SaveOperationalRowsAsync("disponibilidad_contratos", [new DataRow(new Dictionary<string, string?> { ["ContractCode"] = "CTR-1", ["Activo"] = "true" })], CancellationToken.None);
        await using var second = database.NewContext();
        var rows = await second.ReadOperationalRowsAsync("disponibilidad_contratos", CancellationToken.None);
        Assert.Single(rows);
        Assert.Equal("CTR-1", rows[0].GetValue("ContractCode"));
    }
}
