using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderSignatureSignerUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ot_firmas_sql_orden_trabajo_id_vigente",
                table: "ot_firmas_sql");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_orden_trabajo_id_firmante_usuario_id_vigente",
                table: "ot_firmas_sql",
                columns: new[] { "orden_trabajo_id", "firmante_usuario_id", "vigente" },
                unique: true,
                filter: "vigente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ot_firmas_sql_orden_trabajo_id_firmante_usuario_id_vigente",
                table: "ot_firmas_sql");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_orden_trabajo_id_vigente",
                table: "ot_firmas_sql",
                columns: new[] { "orden_trabajo_id", "vigente" },
                unique: true,
                filter: "vigente");
        }
    }
}
