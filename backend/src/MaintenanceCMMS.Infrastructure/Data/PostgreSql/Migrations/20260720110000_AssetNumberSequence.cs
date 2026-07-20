using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute(typeof(CmmsDbContext))]
[Migration("20260720110000_AssetNumberSequence")]
public partial class AssetNumberSequence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS asset_number_seq START WITH 1 INCREMENT BY 1;");
        migrationBuilder.Sql(@"
            SELECT setval(
                'asset_number_seq',
                GREATEST(1, COALESCE((SELECT MAX((substring(codigo FROM '^ACT-([0-9]+)$'))::bigint) FROM activos), 0) + 1),
                false);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SEQUENCE IF EXISTS asset_number_seq;");
    }
}