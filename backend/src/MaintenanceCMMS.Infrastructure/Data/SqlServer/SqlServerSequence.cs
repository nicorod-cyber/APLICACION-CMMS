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
}