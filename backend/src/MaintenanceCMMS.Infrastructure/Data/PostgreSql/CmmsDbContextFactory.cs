using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql;

public sealed class CmmsDbContextFactory : IDesignTimeDbContextFactory<CmmsDbContext>
{
    public CmmsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CMMS_POSTGRES_CONNECTION")
            ?? throw new InvalidOperationException("Configure CMMS_POSTGRES_CONNECTION before running EF Core tools.");
        return new CmmsDbContext(new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(connectionString).Options);
    }
}
