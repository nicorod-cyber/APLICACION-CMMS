using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute(typeof(CmmsDbContext))]
[Migration("20260713160000_MaterialRequestNumberSequence")]
public partial class MaterialRequestNumberSequence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS material_request_number_seq START WITH 1 INCREMENT BY 1;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SEQUENCE IF EXISTS material_request_number_seq;");
    }
}
