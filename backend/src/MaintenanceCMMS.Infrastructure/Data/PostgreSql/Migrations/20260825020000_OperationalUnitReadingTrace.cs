using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations;

[DbContext(typeof(CmmsDbContext))]
[Migration("20260825020000_OperationalUnitReadingTrace")]
public partial class OperationalUnitReadingTrace : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "unidad_operativa_id",
            table: "lecturas_activo",
            type: "uuid",
            nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "es_sincronizacion_composicion",
            table: "lecturas_activo",
            type: "boolean",
            nullable: false,
            defaultValue: false);
        migrationBuilder.CreateIndex(
            name: "ix_lecturas_activo_unidad_operativa_id",
            table: "lecturas_activo",
            column: "unidad_operativa_id");
        migrationBuilder.AddForeignKey(
            name: "fk_lecturas_activo_unidades_operativas_unidad_operativa_id",
            table: "lecturas_activo",
            column: "unidad_operativa_id",
            principalTable: "unidades_operativas",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "fk_lecturas_activo_unidades_operativas_unidad_operativa_id", table: "lecturas_activo");
        migrationBuilder.DropIndex(name: "ix_lecturas_activo_unidad_operativa_id", table: "lecturas_activo");
        migrationBuilder.DropColumn(name: "unidad_operativa_id", table: "lecturas_activo");
        migrationBuilder.DropColumn(name: "es_sincronizacion_composicion", table: "lecturas_activo");
    }
}
