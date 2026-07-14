using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class OperationalUnitAllowedComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reglas_composicion_unidad_activos_permitidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    regla_composicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_activo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    familia_equipo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reglas_composicion_unidad_activos_permitidos", x => x.id);
                    table.CheckConstraint("ck_reglas_composicion_permitidos_objetivo", "tipo_activo_id IS NOT NULL OR familia_equipo_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_reglas_composicion_unidad_activos_permitidos_familias_equip~",
                        column: x => x.familia_equipo_id,
                        principalTable: "familias_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reglas_composicion_unidad_activos_permitidos_reglas_composi~",
                        column: x => x.regla_composicion_id,
                        principalTable: "reglas_composicion_unidad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reglas_composicion_unidad_activos_permitidos_tipos_activo_t~",
                        column: x => x.tipo_activo_id,
                        principalTable: "tipos_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reglas_composicion_unidad_activos_permitidos_familia_equipo~",
                table: "reglas_composicion_unidad_activos_permitidos",
                column: "familia_equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_reglas_composicion_unidad_activos_permitidos_regla_composic~",
                table: "reglas_composicion_unidad_activos_permitidos",
                columns: new[] { "regla_composicion_id", "tipo_activo_id", "familia_equipo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reglas_composicion_unidad_activos_permitidos_tipo_activo_id",
                table: "reglas_composicion_unidad_activos_permitidos",
                column: "tipo_activo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reglas_composicion_unidad_activos_permitidos");
        }
    }
}
