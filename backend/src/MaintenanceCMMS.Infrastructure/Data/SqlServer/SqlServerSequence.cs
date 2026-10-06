using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MaintenanceCMMS.Infrastructure.Data.SqlServer;

public static class SqlServerSequence
{
    private static readonly IReadOnlyDictionary<string, string> Commands = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["asset_number_seq"] = "SELECT NEXT VALUE FOR dbo.asset_number_seq;",
        ["material_request_number_seq"] = "SELECT NEXT VALUE FOR dbo.material_request_number_seq;",
        ["work_notification_number_seq"] = "SELECT NEXT VALUE FOR dbo.work_notification_number_seq;",
        ["work_order_number_seq"] = "SELECT NEXT VALUE FOR dbo.work_order_number_seq;",
        ["spare_part_number_seq"] = "SELECT NEXT VALUE FOR dbo.spare_part_number_seq;",
        ["stock_movement_number_seq"] = "SELECT NEXT VALUE FOR dbo.stock_movement_number_seq;",
        ["stock_reservation_number_seq"] = "SELECT NEXT VALUE FOR dbo.stock_reservation_number_seq;",
        ["stock_transfer_number_seq"] = "SELECT NEXT VALUE FOR dbo.stock_transfer_number_seq;"
    };

    public static async Task<long> NextValueAsync(CmmsDbContext dbContext, string sequenceName, CancellationToken cancellationToken)
    {
        if (!Commands.TryGetValue(sequenceName, out var commandText))
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceName), sequenceName, "Unknown SQL Server sequence.");
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
            command.CommandText = commandText;
            if (dbContext.Database.CurrentTransaction is not null)
            {
                command.Transaction = dbContext.Database.CurrentTransaction.GetDbTransaction();
            }

            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Allocates an Aviso number while holding a transaction-scoped SQL Server application lock.
    /// The lock makes an imported historical number and the SQL sequence advance as one serialized
    /// allocation operation, without relying on Count()+1.
    /// </summary>
    public static async Task<long> NextWorkNotificationValueAsync(CmmsDbContext dbContext, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("La asignación de avisos requiere una transacción activa.");
        }

        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await connection.OpenAsync(cancellationToken);

        try
        {
            async Task<object?> ScalarAsync(string sql)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Transaction = dbContext.Database.CurrentTransaction.GetDbTransaction();
                return await command.ExecuteScalarAsync(cancellationToken);
            }

            var lockResult = Convert.ToInt32(await ScalarAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = N'cmms.work_notification_number',
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Transaction',
                    @LockTimeout = 10000;
                SELECT @result;
                """), CultureInfo.InvariantCulture);
            if (lockResult < 0) throw new InvalidOperationException("No fue posible reservar un número de aviso.");

            var historicalMaximum = Convert.ToInt64(await ScalarAsync("""
                SELECT ISNULL(MAX(TRY_CONVERT(bigint, SUBSTRING(aviso_id, 4, 32))), 0)
                FROM dbo.avisos_trabajo_sql
                WHERE aviso_id LIKE N'AV-%';
                """), CultureInfo.InvariantCulture);
            var currentValue = await ScalarAsync("SELECT current_value FROM sys.sequences WHERE object_id = OBJECT_ID(N'dbo.work_notification_number_seq');");
            var nextMinimum = checked(historicalMaximum + 1);
            var sequenceCurrent = currentValue is null || currentValue is DBNull ? 0L : Convert.ToInt64(currentValue, CultureInfo.InvariantCulture);

            if (sequenceCurrent < historicalMaximum)
            {
                // The number is generated solely from database-derived numeric values.
                await ScalarAsync($"ALTER SEQUENCE dbo.work_notification_number_seq RESTART WITH {nextMinimum.ToString(CultureInfo.InvariantCulture)};");
            }

            return Convert.ToInt64(await ScalarAsync("SELECT NEXT VALUE FOR dbo.work_notification_number_seq;"), CultureInfo.InvariantCulture);
        }
        finally
        {
            if (closeConnection) await connection.CloseAsync();
        }
    }
}
