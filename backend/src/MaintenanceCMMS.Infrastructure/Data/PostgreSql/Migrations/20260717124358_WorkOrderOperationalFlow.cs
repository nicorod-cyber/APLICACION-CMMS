using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderOperationalFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM ot_tecnicos_tarea_sql)
                       OR EXISTS (SELECT 1 FROM ot_hh_sql)
                       OR EXISTS (SELECT 1 FROM ot_evidencias_sql)
                       OR EXISTS (SELECT 1 FROM ot_firmas_sql)
                       OR EXISTS (SELECT 1 FROM tareas_ot_sql)
                       OR EXISTS (SELECT 1 FROM ot_checklists_sql) THEN
                        RAISE EXCEPTION 'WorkOrderOperationalFlow requires empty legacy OT tables; migrate identities explicitly before applying this migration.';
                    END IF;
                END $$;

                ALTER TABLE ot_firmas_sql DROP CONSTRAINT IF EXISTS fk_ot_firmas_tarea;
                ALTER TABLE ot_firmas_sql DROP CONSTRAINT IF EXISTS "FK_ot_firmas_sql_tareas_ot_sql_tarea_id";
                DROP INDEX IF EXISTS "IX_ot_firmas_sql_orden_trabajo_id";
                DROP INDEX IF EXISTS "IX_ot_evidencias_sql_tarea_id";
                DROP INDEX IF EXISTS "IX_ot_firmas_sql_tarea_id";
            """);

            migrationBuilder.DropTable(
                name: "ot_tecnicos_tarea_sql");


            migrationBuilder.DropColumn(
                name: "alcance",
                table: "ot_firmas_sql");

            migrationBuilder.DropColumn(
                name: "signature_file_key",
                table: "ot_firmas_sql");

            migrationBuilder.DropColumn(
                name: "creado_por_usuario_id",
                table: "ot_evidencias_sql");

            migrationBuilder.RenameColumn(
                name: "tarea_id",
                table: "ot_firmas_sql",
                newName: "invalidada_por_usuario_id");


            migrationBuilder.RenameColumn(
                name: "creado_por_usuario_at_utc",
                table: "ot_evidencias_sql",
                newName: "subido_at_utc");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "tareas_ot_sql",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByUserId",
                table: "tareas_ot_sql",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "aprobada_por_usuario_id",
                table: "tareas_ot_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "aprobada_supervisor_utc",
                table: "tareas_ot_sql",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelada_por_usuario_id",
                table: "tareas_ot_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelada_utc",
                table: "tareas_ot_sql",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "completada_por_usuario_id",
                table: "tareas_ot_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completada_tecnico_utc",
                table: "tareas_ot_sql",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "criterio_aceptacion",
                table: "tareas_ot_sql",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "estado_id",
                table: "tareas_ot_sql",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "horas_estimadas",
                table: "tareas_ot_sql",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "inicio_real_utc",
                table: "tareas_ot_sql",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "item_plantilla_preventiva_id",
                table: "tareas_ot_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_cancelacion",
                table: "tareas_ot_sql",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_observacion",
                table: "tareas_ot_sql",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "obligatoria_preventiva",
                table: "tareas_ot_sql",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "observada_por_usuario_id",
                table: "tareas_ot_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "observada_utc",
                table: "tareas_ot_sql",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origen",
                table: "tareas_ot_sql",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "plantilla_preventiva_id",
                table: "tareas_ot_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "plantilla_preventiva_version_snapshot",
                table: "tareas_ot_sql",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "titulo",
                table: "tareas_ot_sql",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("ALTER TABLE ot_hh_sql ALTER COLUMN validado_por_usuario_id TYPE uuid USING validado_por_usuario_id::uuid;");

            migrationBuilder.Sql("ALTER TABLE ot_hh_sql ALTER COLUMN tecnico_usuario_id TYPE uuid USING tecnico_usuario_id::uuid;");

            migrationBuilder.Sql("ALTER TABLE ot_hh_sql ALTER COLUMN registrado_por_usuario_id TYPE uuid USING registrado_por_usuario_id::uuid;");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "anulado_at_utc",
                table: "ot_hh_sql",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "anulado_por_usuario_id",
                table: "ot_hh_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_anulacion",
                table: "ot_hh_sql",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql("ALTER TABLE ot_firmas_sql ALTER COLUMN firmante_usuario_id TYPE uuid USING firmante_usuario_id::uuid;");

            migrationBuilder.AddColumn<string>(
                name: "contenido_hash",
                table: "ot_firmas_sql",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invalidada_at_utc",
                table: "ot_firmas_sql",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_invalidacion",
                table: "ot_firmas_sql",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version_contenido",
                table: "ot_firmas_sql",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "tarea_id",
                table: "ot_evidencias_sql",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "anulado_at_utc",
                table: "ot_evidencias_sql",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "anulado_por_usuario_id",
                table: "ot_evidencias_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "capturada_at_utc",
                table: "ot_evidencias_sql",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_anulacion",
                table: "ot_evidencias_sql",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "subido_por_usuario_id",
                table: "ot_evidencias_sql",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "motivo_reasignacion_supervisor",
                table: "ordenes_trabajo_sql",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "plantilla_preventiva_id",
                table: "ordenes_trabajo_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "plantilla_preventiva_version_snapshot",
                table: "ordenes_trabajo_sql",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "supervisor_asignado_at_utc",
                table: "ordenes_trabajo_sql",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supervisor_asignado_por_usuario_id",
                table: "ordenes_trabajo_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supervisor_nombre_snapshot",
                table: "ordenes_trabajo_sql",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supervisor_usuario_id",
                table: "ordenes_trabajo_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ot_tecnicos_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tecnico_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tecnico_nombre_snapshot = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    asignado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    asignado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    desasignado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    desasignado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_desasignacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_tecnicos_sql", x => x.id);
                    table.ForeignKey(
                        name: "fk_ot_tecnicos_asignador",
                        column: x => x.asignado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ot_tecnicos_desasignador",
                        column: x => x.desasignado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ot_tecnicos_ot",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ot_tecnicos_usuario",
                        column: x => x.tecnico_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tareas_ot_estado_historial_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_anterior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_nuevo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tareas_ot_estado_historial_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_tareas_ot_estado_historial_sql_catalogos_trabajo_estado_ant~",
                        column: x => x.estado_anterior_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tareas_ot_estado_historial_sql_catalogos_trabajo_estado_nue~",
                        column: x => x.estado_nuevo_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tareas_ot_estado_historial_sql_tareas_ot_sql_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas_ot_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tareas_ot_estado_historial_sql_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tareas_ot_sql_aprobada_por_usuario_id",
                table: "tareas_ot_sql",
                column: "aprobada_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_ot_sql_cancelada_por_usuario_id",
                table: "tareas_ot_sql",
                column: "cancelada_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_ot_sql_completada_por_usuario_id",
                table: "tareas_ot_sql",
                column: "completada_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_ot_sql_estado_id",
                table: "tareas_ot_sql",
                column: "estado_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_ot_sql_observada_por_usuario_id",
                table: "tareas_ot_sql",
                column: "observada_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_anulado_por_usuario_id",
                table: "ot_hh_sql",
                column: "anulado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_registrado_por_usuario_id",
                table: "ot_hh_sql",
                column: "registrado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_tecnico_usuario_id_fecha_trabajo_utc",
                table: "ot_hh_sql",
                columns: new[] { "tecnico_usuario_id", "fecha_trabajo_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_validado_por_usuario_id",
                table: "ot_hh_sql",
                column: "validado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_firmante_usuario_id",
                table: "ot_firmas_sql",
                column: "firmante_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_orden_trabajo_id_vigente",
                table: "ot_firmas_sql",
                columns: new[] { "orden_trabajo_id", "vigente" },
                unique: true,
                filter: "vigente");

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_anulado_por_usuario_id",
                table: "ot_evidencias_sql",
                column: "anulado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_subido_por_usuario_id",
                table: "ot_evidencias_sql",
                column: "subido_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_tarea_id_vigente",
                table: "ot_evidencias_sql",
                columns: new[] { "tarea_id", "vigente" });

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_plantilla_preventiva_id",
                table: "ordenes_trabajo_sql",
                column: "plantilla_preventiva_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_supervisor_asignado_por_usuario_id",
                table: "ordenes_trabajo_sql",
                column: "supervisor_asignado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_supervisor_usuario_id",
                table: "ordenes_trabajo_sql",
                column: "supervisor_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_tecnicos_sql_asignado_por_usuario_id",
                table: "ot_tecnicos_sql",
                column: "asignado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_tecnicos_sql_desasignado_por_usuario_id",
                table: "ot_tecnicos_sql",
                column: "desasignado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_ot_tecnicos_usuario_vigente",
                table: "ot_tecnicos_sql",
                columns: new[] { "tecnico_usuario_id", "vigente" });

            migrationBuilder.CreateIndex(
                name: "uq_ot_tecnicos_ot_usuario_vigente",
                table: "ot_tecnicos_sql",
                columns: new[] { "orden_trabajo_id", "tecnico_usuario_id" },
                unique: true,
                filter: "vigente");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_ot_estado_historial_sql_estado_anterior_id",
                table: "tareas_ot_estado_historial_sql",
                column: "estado_anterior_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_ot_estado_historial_sql_estado_nuevo_id",
                table: "tareas_ot_estado_historial_sql",
                column: "estado_nuevo_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_ot_estado_historial_sql_tarea_id_fecha_utc",
                table: "tareas_ot_estado_historial_sql",
                columns: new[] { "tarea_id", "fecha_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_tareas_ot_estado_historial_sql_usuario_id",
                table: "tareas_ot_estado_historial_sql",
                column: "usuario_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ot_plantilla_preventiva",
                table: "ordenes_trabajo_sql",
                column: "plantilla_preventiva_id",
                principalTable: "plantillas_checklist",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ot_supervisor",
                table: "ordenes_trabajo_sql",
                column: "supervisor_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ot_supervisor_asignador",
                table: "ordenes_trabajo_sql",
                column: "supervisor_asignado_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ot_evidencias_sql_usuarios_anulado_por_usuario_id",
                table: "ot_evidencias_sql",
                column: "anulado_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ot_evidencias_sql_usuarios_subido_por_usuario_id",
                table: "ot_evidencias_sql",
                column: "subido_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ot_firmas_sql_usuarios_firmante_usuario_id",
                table: "ot_firmas_sql",
                column: "firmante_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ot_firmas_sql_usuarios_invalidada_por_usuario_id",
                table: "ot_firmas_sql",
                column: "invalidada_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ot_hh_sql_usuarios_anulado_por_usuario_id",
                table: "ot_hh_sql",
                column: "anulado_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ot_hh_sql_usuarios_registrado_por_usuario_id",
                table: "ot_hh_sql",
                column: "registrado_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ot_hh_sql_usuarios_tecnico_usuario_id",
                table: "ot_hh_sql",
                column: "tecnico_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ot_hh_sql_usuarios_validado_por_usuario_id",
                table: "ot_hh_sql",
                column: "validado_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tareas_ot_aprobada_por",
                table: "tareas_ot_sql",
                column: "aprobada_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tareas_ot_cancelada_por",
                table: "tareas_ot_sql",
                column: "cancelada_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tareas_ot_completada_por",
                table: "tareas_ot_sql",
                column: "completada_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tareas_ot_estado",
                table: "tareas_ot_sql",
                column: "estado_id",
                principalTable: "catalogos_trabajo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tareas_ot_observada_por",
                table: "tareas_ot_sql",
                column: "observada_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ot_plantilla_preventiva",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropForeignKey(
                name: "fk_ot_supervisor",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropForeignKey(
                name: "fk_ot_supervisor_asignador",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropForeignKey(
                name: "FK_ot_evidencias_sql_usuarios_anulado_por_usuario_id",
                table: "ot_evidencias_sql");

            migrationBuilder.DropForeignKey(
                name: "FK_ot_evidencias_sql_usuarios_subido_por_usuario_id",
                table: "ot_evidencias_sql");

            migrationBuilder.DropForeignKey(
                name: "FK_ot_firmas_sql_usuarios_firmante_usuario_id",
                table: "ot_firmas_sql");

            migrationBuilder.DropForeignKey(
                name: "FK_ot_firmas_sql_usuarios_invalidada_por_usuario_id",
                table: "ot_firmas_sql");

            migrationBuilder.DropForeignKey(
                name: "FK_ot_hh_sql_usuarios_anulado_por_usuario_id",
                table: "ot_hh_sql");

            migrationBuilder.DropForeignKey(
                name: "FK_ot_hh_sql_usuarios_registrado_por_usuario_id",
                table: "ot_hh_sql");

            migrationBuilder.DropForeignKey(
                name: "FK_ot_hh_sql_usuarios_tecnico_usuario_id",
                table: "ot_hh_sql");

            migrationBuilder.DropForeignKey(
                name: "FK_ot_hh_sql_usuarios_validado_por_usuario_id",
                table: "ot_hh_sql");

            migrationBuilder.DropForeignKey(
                name: "fk_tareas_ot_aprobada_por",
                table: "tareas_ot_sql");

            migrationBuilder.DropForeignKey(
                name: "fk_tareas_ot_cancelada_por",
                table: "tareas_ot_sql");

            migrationBuilder.DropForeignKey(
                name: "fk_tareas_ot_completada_por",
                table: "tareas_ot_sql");

            migrationBuilder.DropForeignKey(
                name: "fk_tareas_ot_estado",
                table: "tareas_ot_sql");

            migrationBuilder.DropForeignKey(
                name: "fk_tareas_ot_observada_por",
                table: "tareas_ot_sql");

            migrationBuilder.DropTable(
                name: "ot_tecnicos_sql");

            migrationBuilder.DropTable(
                name: "tareas_ot_estado_historial_sql");

            migrationBuilder.DropIndex(
                name: "IX_tareas_ot_sql_aprobada_por_usuario_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropIndex(
                name: "IX_tareas_ot_sql_cancelada_por_usuario_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropIndex(
                name: "IX_tareas_ot_sql_completada_por_usuario_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropIndex(
                name: "IX_tareas_ot_sql_estado_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropIndex(
                name: "IX_tareas_ot_sql_observada_por_usuario_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropIndex(
                name: "IX_ot_hh_sql_anulado_por_usuario_id",
                table: "ot_hh_sql");

            migrationBuilder.DropIndex(
                name: "IX_ot_hh_sql_registrado_por_usuario_id",
                table: "ot_hh_sql");

            migrationBuilder.DropIndex(
                name: "IX_ot_hh_sql_tecnico_usuario_id_fecha_trabajo_utc",
                table: "ot_hh_sql");

            migrationBuilder.DropIndex(
                name: "IX_ot_hh_sql_validado_por_usuario_id",
                table: "ot_hh_sql");

            migrationBuilder.DropIndex(
                name: "IX_ot_firmas_sql_firmante_usuario_id",
                table: "ot_firmas_sql");

            migrationBuilder.DropIndex(
                name: "IX_ot_firmas_sql_orden_trabajo_id_vigente",
                table: "ot_firmas_sql");

            migrationBuilder.DropIndex(
                name: "IX_ot_evidencias_sql_anulado_por_usuario_id",
                table: "ot_evidencias_sql");

            migrationBuilder.DropIndex(
                name: "IX_ot_evidencias_sql_subido_por_usuario_id",
                table: "ot_evidencias_sql");

            migrationBuilder.DropIndex(
                name: "IX_ot_evidencias_sql_tarea_id_vigente",
                table: "ot_evidencias_sql");

            migrationBuilder.DropIndex(
                name: "IX_ordenes_trabajo_sql_plantilla_preventiva_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropIndex(
                name: "IX_ordenes_trabajo_sql_supervisor_asignado_por_usuario_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropIndex(
                name: "IX_ordenes_trabajo_sql_supervisor_usuario_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "aprobada_por_usuario_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "aprobada_supervisor_utc",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "cancelada_por_usuario_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "cancelada_utc",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "completada_por_usuario_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "completada_tecnico_utc",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "criterio_aceptacion",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "estado_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "horas_estimadas",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "inicio_real_utc",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "item_plantilla_preventiva_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "motivo_cancelacion",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "motivo_observacion",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "obligatoria_preventiva",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "observada_por_usuario_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "observada_utc",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "origen",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "plantilla_preventiva_id",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "plantilla_preventiva_version_snapshot",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "titulo",
                table: "tareas_ot_sql");

            migrationBuilder.DropColumn(
                name: "anulado_at_utc",
                table: "ot_hh_sql");

            migrationBuilder.DropColumn(
                name: "anulado_por_usuario_id",
                table: "ot_hh_sql");

            migrationBuilder.DropColumn(
                name: "motivo_anulacion",
                table: "ot_hh_sql");

            migrationBuilder.DropColumn(
                name: "contenido_hash",
                table: "ot_firmas_sql");

            migrationBuilder.DropColumn(
                name: "invalidada_at_utc",
                table: "ot_firmas_sql");

            migrationBuilder.DropColumn(
                name: "motivo_invalidacion",
                table: "ot_firmas_sql");

            migrationBuilder.DropColumn(
                name: "version_contenido",
                table: "ot_firmas_sql");

            migrationBuilder.DropColumn(
                name: "anulado_at_utc",
                table: "ot_evidencias_sql");

            migrationBuilder.DropColumn(
                name: "anulado_por_usuario_id",
                table: "ot_evidencias_sql");

            migrationBuilder.DropColumn(
                name: "capturada_at_utc",
                table: "ot_evidencias_sql");

            migrationBuilder.DropColumn(
                name: "motivo_anulacion",
                table: "ot_evidencias_sql");

            migrationBuilder.DropColumn(
                name: "subido_por_usuario_id",
                table: "ot_evidencias_sql");

            migrationBuilder.DropColumn(
                name: "motivo_reasignacion_supervisor",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "plantilla_preventiva_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "plantilla_preventiva_version_snapshot",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "supervisor_asignado_at_utc",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "supervisor_asignado_por_usuario_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "supervisor_nombre_snapshot",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "supervisor_usuario_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.RenameColumn(
                name: "invalidada_por_usuario_id",
                table: "ot_firmas_sql",
                newName: "tarea_id");


            migrationBuilder.RenameColumn(
                name: "subido_at_utc",
                table: "ot_evidencias_sql",
                newName: "creado_por_usuario_at_utc");

            migrationBuilder.AlterColumn<string>(
                name: "validado_por_usuario_id",
                table: "ot_hh_sql",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "tecnico_usuario_id",
                table: "ot_hh_sql",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "registrado_por_usuario_id",
                table: "ot_hh_sql",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "firmante_usuario_id",
                table: "ot_firmas_sql",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "alcance",
                table: "ot_firmas_sql",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "signature_file_key",
                table: "ot_firmas_sql",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "tarea_id",
                table: "ot_evidencias_sql",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "creado_por_usuario_id",
                table: "ot_evidencias_sql",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ot_tecnicos_tarea_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asignado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    asignado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    tecnico_nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    tecnico_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    desasignado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    desasignado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    motivo_desasignacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ot_tecnicos_tarea_sql_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_ot_tecnicos_ot",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ot_tecnicos_tarea",
                        column: x => x.tarea_id,
                        principalTable: "tareas_ot_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });


            migrationBuilder.CreateIndex(
                name: "ix_ot_tecnicos_tarea_sql_tarea_tecnico_vigente",
                table: "ot_tecnicos_tarea_sql",
                columns: new[] { "tarea_id", "tecnico_usuario_id", "vigente" },
                unique: true,
                filter: "vigente");

            migrationBuilder.AddForeignKey(
                name: "fk_ot_firmas_tarea",
                table: "ot_firmas_sql",
                column: "tarea_id",
                principalTable: "tareas_ot_sql",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
