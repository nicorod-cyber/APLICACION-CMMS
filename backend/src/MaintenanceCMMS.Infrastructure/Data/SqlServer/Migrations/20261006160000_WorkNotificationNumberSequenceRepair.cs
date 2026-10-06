using MaintenanceCMMS.Infrastructure.Data.SqlServer;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.SqlServer.Migrations;

/// <summary>
/// Moves the Aviso sequence forward without altering historic numbers. This is intentionally
/// data-aware because installations can already contain AV-###### values before this sequence.
/// </summary>
[DbContext(typeof(CmmsDbContext))]
[Migration("20261006160000_WorkNotificationNumberSequenceRepair")]
public partial class WorkNotificationNumberSequenceRepair : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE @maxHistorical bigint = (
                SELECT ISNULL(MAX(TRY_CONVERT(bigint, SUBSTRING(aviso_id, 4, 32))), 0)
                FROM dbo.avisos_trabajo_sql
                WHERE aviso_id LIKE N'AV-%');
            DECLARE @currentSequence bigint;
            SELECT @currentSequence = TRY_CONVERT(bigint, current_value)
            FROM sys.sequences
            WHERE object_id = OBJECT_ID(N'dbo.work_notification_number_seq');
            SET @currentSequence = ISNULL(@currentSequence, 0);
            DECLARE @restartWith bigint = CASE
                WHEN @currentSequence > @maxHistorical THEN @currentSequence + 1
                ELSE @maxHistorical + 1
            END;
            DECLARE @command nvarchar(max) =
                N'ALTER SEQUENCE dbo.work_notification_number_seq RESTART WITH ' +
                CONVERT(nvarchar(30), @restartWith) + N';';
            EXEC sys.sp_executesql @command;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Sequence values are intentionally never moved backward: doing so could reuse a number.
    }
}
