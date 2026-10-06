using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AvisosRedesignPhaseOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_avisos_trabajo_sql_orden_trabajo_id",
                table: "avisos_trabajo_sql");

            migrationBuilder.AddColumn<string>(
                name: "estado_operacional",
                table: "avisos_trabajo_sql",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fuera_servicio_desde_utc",
                table: "avisos_trabajo_sql",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "lectura_medidor",
                table: "avisos_trabajo_sql",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lectura_medidor_id",
                table: "avisos_trabajo_sql",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "restriccion_operacional",
                table: "avisos_trabajo_sql",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "avisos_trabajo_historial_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    aviso_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado_origen = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    estado_destino = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avisos_trabajo_historial_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_historial_sql_avisos_trabajo_sql_aviso_id",
                        column: x => x.aviso_id,
                        principalTable: "avisos_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avisos_trabajo_items_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    aviso_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    secuencia = table.Column<int>(type: "int", nullable: false),
                    activo_afectado_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_codigo_snapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    activo_nombre_snapshot = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    rol_componente_snapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    sistema_tecnico_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    subsistema_tecnico_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    componente_tecnico_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    tarea_ot_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avisos_trabajo_items_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_items_sql_activos_activo_afectado_id",
                        column: x => x.activo_afectado_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_items_sql_avisos_trabajo_sql_aviso_id",
                        column: x => x.aviso_id,
                        principalTable: "avisos_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_items_sql_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_items_sql_tareas_ot_sql_tarea_ot_id",
                        column: x => x.tarea_ot_id,
                        principalTable: "tareas_ot_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avisos_trabajo_evidencias_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    aviso_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    archivo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    subido_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avisos_trabajo_evidencias_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_evidencias_sql_archivos_archivo_id",
                        column: x => x.archivo_id,
                        principalTable: "archivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_evidencias_sql_avisos_trabajo_items_sql_aviso_item_id",
                        column: x => x.aviso_item_id,
                        principalTable: "avisos_trabajo_items_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Keep legacy headers and map each resolvable notice to one synthetic item.
            // No historical columns or records are removed in this phase.
            migrationBuilder.Sql("""
                INSERT INTO dbo.catalogos_trabajo (id, categoria, codigo, nombre, activo, orden, created_at_utc)
                SELECT NEWID(), 'WorkNotificationStatus', v.codigo, v.nombre, 1, v.orden, SYSUTCDATETIME()
                FROM (VALUES ('Borrador','Borrador',1),('PendientePlanificacion','Pendiente planificación',2),('DevueltoFaena','Devuelto a faena',3),('EnGestion','En gestión',4),('Cerrado','Cerrado',5),('Rechazado','Rechazado',6),('Anulado','Anulado',7)) v(codigo,nombre,orden)
                WHERE NOT EXISTS (SELECT 1 FROM dbo.catalogos_trabajo c WHERE c.categoria='WorkNotificationStatus' AND c.codigo=v.codigo);

                UPDATE n SET estado_id = destino.id,
                    estado_operacional = COALESCE(n.estado_operacional, 'Operativo')
                FROM dbo.avisos_trabajo_sql n
                INNER JOIN dbo.catalogos_trabajo origen ON origen.id=n.estado_id
                INNER JOIN dbo.catalogos_trabajo destino ON destino.categoria='WorkNotificationStatus' AND destino.codigo = CASE origen.codigo
                    WHEN 'Aprobado' THEN 'EnGestion' WHEN 'ConvertidoOT' THEN 'Cerrado' WHEN 'Rechazado' THEN 'Rechazado' WHEN 'Anulado' THEN 'Anulado' ELSE 'PendientePlanificacion' END;

                ;WITH resolucion AS (
                    SELECT n.id aviso_id, COALESCE(n.activo_id, componente.activo_id) activo_id
                    FROM dbo.avisos_trabajo_sql n
                    OUTER APPLY (SELECT TOP 1 c.activo_id FROM dbo.componentes_unidad_operativa c WHERE c.unidad_operativa_id=n.unidad_operativa_id AND c.fecha_desmontaje_utc IS NULL ORDER BY c.fecha_montaje_utc DESC) componente
                )
                INSERT INTO dbo.avisos_trabajo_items_sql (id,aviso_id,secuencia,activo_afectado_id,activo_codigo_snapshot,activo_nombre_snapshot,descripcion,estado,created_at_utc)
                SELECT NEWID(), n.id, 1, r.activo_id, a.codigo, a.nombre, n.descripcion, 'PendientePlanificacion', n.created_at_utc
                FROM dbo.avisos_trabajo_sql n
                INNER JOIN resolucion r ON r.aviso_id=n.id
                INNER JOIN dbo.activos a ON a.id=r.activo_id
                WHERE r.activo_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.avisos_trabajo_items_sql i WHERE i.aviso_id=n.id);
                """);

            migrationBuilder.DropCheckConstraint(name: "ck_lecturas_activo_origen", table: "lecturas_activo");
            migrationBuilder.AddCheckConstraint(name: "ck_lecturas_activo_origen", table: "lecturas_activo", sql: "origen IN ('MANUAL','ORDEN_TRABAJO','IMPORTACION','SAP','TELEMETRIA','AVISO')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_avisos_estado_operacional",
                table: "avisos_trabajo_sql",
                sql: "estado_operacional IS NULL OR estado_operacional IN ('Operativo','OperativoConAlerta','FueraDeServicio')");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_evidencias_sql_archivo_id",
                table: "avisos_trabajo_evidencias_sql",
                column: "archivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_evidencias_sql_aviso_item_id_archivo_id",
                table: "avisos_trabajo_evidencias_sql",
                columns: new[] { "aviso_item_id", "archivo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_historial_sql_aviso_id_fecha_utc",
                table: "avisos_trabajo_historial_sql",
                columns: new[] { "aviso_id", "fecha_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_items_sql_activo_afectado_id",
                table: "avisos_trabajo_items_sql",
                column: "activo_afectado_id");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_items_sql_aviso_id_secuencia",
                table: "avisos_trabajo_items_sql",
                columns: new[] { "aviso_id", "secuencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_items_sql_orden_trabajo_id",
                table: "avisos_trabajo_items_sql",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_items_sql_tarea_ot_id",
                table: "avisos_trabajo_items_sql",
                column: "tarea_ot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avisos_trabajo_evidencias_sql");

            migrationBuilder.DropTable(
                name: "avisos_trabajo_historial_sql");

            migrationBuilder.DropTable(
                name: "avisos_trabajo_items_sql");

            migrationBuilder.DropIndex(
                name: "IX_avisos_trabajo_sql_orden_trabajo_id",
                table: "avisos_trabajo_sql");

            migrationBuilder.DropCheckConstraint(
                name: "ck_avisos_estado_operacional",
                table: "avisos_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "estado_operacional",
                table: "avisos_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "fuera_servicio_desde_utc",
                table: "avisos_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "lectura_medidor",
                table: "avisos_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "lectura_medidor_id",
                table: "avisos_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "restriccion_operacional",
                table: "avisos_trabajo_sql");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_orden_trabajo_id",
                table: "avisos_trabajo_sql",
                column: "orden_trabajo_id",
                unique: true,
                filter: "orden_trabajo_id IS NOT NULL");
        }
    }
}
