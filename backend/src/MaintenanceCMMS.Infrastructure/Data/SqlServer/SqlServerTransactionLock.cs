using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MaintenanceCMMS.Infrastructure.Data.SqlServer;

public static class SqlServerTransactionLock
{
    public static async Task AcquireExclusiveAsync(CmmsDbContext dbContext, string resource, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("sp_getapplock must be acquired inside the active database transaction.");
        }

        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction.GetDbTransaction();
            command.CommandText = "DECLARE @lockResult int; EXEC @lockResult = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; SELECT @lockResult;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@resource";
            parameter.DbType = DbType.String;
            parameter.Value = resource;
            command.Parameters.Add(parameter);
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            if (result < 0)
            {
                throw new InvalidOperationException($"Could not acquire SQL Server application lock '{resource}'. Result: {result}.");
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}