using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql;

public sealed class CmmsDbContextFactory : IDesignTimeDbContextFactory<CmmsDbContext>
{
    public CmmsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CMMS_POSTGRES_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=cmms;Username=cmms_app;Password=cmms_app_password";
        return new CmmsDbContext(new DbContextOptionsBuilder<CmmsDbContext>().UseNpgsql(connectionString).Options);
    }
}
