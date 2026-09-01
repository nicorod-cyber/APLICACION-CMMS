using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MaintenanceCMMS.Infrastructure.Data.SqlServer;

public sealed class CmmsDbContextFactory : IDesignTimeDbContextFactory<CmmsDbContext>
{
    public CmmsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CMMS_SQLSERVER_CONNECTION")
            ?? throw new InvalidOperationException("Configure CMMS_SQLSERVER_CONNECTION before running EF Core tools.");
        return new CmmsDbContext(new DbContextOptionsBuilder<CmmsDbContext>().UseSqlServer(connectionString).Options);
    }
}
