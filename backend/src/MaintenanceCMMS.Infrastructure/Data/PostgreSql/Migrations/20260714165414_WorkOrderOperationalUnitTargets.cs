using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderOperationalUnitTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "activo_id",
                table: "ordenes_trabajo_sql",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "unidad_operativa_id",
                table: "ordenes_trabajo_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "unidad_operativa_id",
                table: "avisos_trabajo_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "orden_trabajo_activos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    activo_codigo_snapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    activo_nombre_snapshot = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    agregado_en_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    agregado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orden_trabajo_activos", x => x.id);
                    table.CheckConstraint("ck_orden_trabajo_activos_rol", "rol IN ('PRINCIPAL','AFECTADO','MONTAJE','DESMONTAJE')");
                    table.ForeignKey(
                        name: "FK_orden_trabajo_activos_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orden_trabajo_activos_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
                INSERT INTO orden_trabajo_activos (id, orden_trabajo_id, activo_id, rol, activo_codigo_snapshot, activo_nombre_snapshot, agregado_en_utc, agregado_por_usuario_id, created_at_utc)
                SELECT gen_random_uuid(), ot.id, a.id, 'PRINCIPAL', a.codigo, a.nombre, ot.created_at_utc, ot.creado_por_usuario_id, now()
                FROM ordenes_trabajo_sql ot
                INNER JOIN activos a ON a.id = ot.activo_id
                WHERE NOT EXISTS (SELECT 1 FROM orden_trabajo_activos ota WHERE ota.orden_trabajo_id = ot.id AND ota.activo_id = a.id);
            ");
            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_unidad_operativa_id",
                table: "ordenes_trabajo_sql",
                column: "unidad_operativa_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ordenes_trabajo_sql_objetivo",
                table: "ordenes_trabajo_sql",
                sql: "activo_id IS NOT NULL OR unidad_operativa_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_unidad_operativa_id",
                table: "avisos_trabajo_sql",
                column: "unidad_operativa_id");

            migrationBuilder.CreateIndex(
                name: "IX_orden_trabajo_activos_activo_id",
                table: "orden_trabajo_activos",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_orden_trabajo_activos_orden_trabajo_id_activo_id",
                table: "orden_trabajo_activos",
                columns: new[] { "orden_trabajo_id", "activo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orden_trabajo_activos_orden_trabajo_id_rol",
                table: "orden_trabajo_activos",
                columns: new[] { "orden_trabajo_id", "rol" });

            migrationBuilder.AddForeignKey(
                name: "FK_avisos_trabajo_sql_unidades_operativas_unidad_operativa_id",
                table: "avisos_trabajo_sql",
                column: "unidad_operativa_id",
                principalTable: "unidades_operativas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ordenes_trabajo_sql_unidades_operativas_unidad_operativa_id",
                table: "ordenes_trabajo_sql",
                column: "unidad_operativa_id",
                principalTable: "unidades_operativas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM ordenes_trabajo_sql WHERE activo_id IS NULL) THEN
                        RAISE EXCEPTION 'No es posible revertir: existen OT cuyo objetivo es solo una unidad operativa.';
                    END IF;
                END $$;
            ");
            migrationBuilder.DropForeignKey(
                name: "FK_avisos_trabajo_sql_unidades_operativas_unidad_operativa_id",
                table: "avisos_trabajo_sql");

            migrationBuilder.DropForeignKey(
                name: "FK_ordenes_trabajo_sql_unidades_operativas_unidad_operativa_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropTable(
                name: "orden_trabajo_activos");

            migrationBuilder.DropIndex(
                name: "IX_ordenes_trabajo_sql_unidad_operativa_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ordenes_trabajo_sql_objetivo",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropIndex(
                name: "IX_avisos_trabajo_sql_unidad_operativa_id",
                table: "avisos_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "unidad_operativa_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "unidad_operativa_id",
                table: "avisos_trabajo_sql");

            migrationBuilder.AlterColumn<Guid>(
                name: "activo_id",
                table: "ordenes_trabajo_sql",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

