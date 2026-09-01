using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Data.SqlServer;

/// <summary>
/// Runs a complete unit of work through the SQL Server execution strategy.
/// Explicit transactions must be created by the caller inside the supplied
/// delegate so retries never run a transaction outside its strategy.
/// </summary>
internal static class SqlServerExecutionStrategy
{
    public static Task ExecuteAsync(CmmsDbContext db, Func<Task> operation) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(operation);

    public static Task<TResult> ExecuteAsync<TResult>(CmmsDbContext db, Func<Task<TResult>> operation) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(operation);
}
