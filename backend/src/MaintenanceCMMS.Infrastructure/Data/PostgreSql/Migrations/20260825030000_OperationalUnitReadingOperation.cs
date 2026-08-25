using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations;

[DbContext(typeof(CmmsDbContext))]
[Migration("20260825030000_OperationalUnitReadingOperation")]
public partial class OperationalUnitReadingOperation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "operacion_lectura_unidad_id", table: "lecturas_activo", type: "uuid", nullable: true);
        migrationBuilder.CreateIndex(name: "ix_lecturas_activo_operacion_lectura_unidad_id", table: "lecturas_activo", column: "operacion_lectura_unidad_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ix_lecturas_activo_operacion_lectura_unidad_id", table: "lecturas_activo");
        migrationBuilder.DropColumn(name: "operacion_lectura_unidad_id", table: "lecturas_activo");
    }
}