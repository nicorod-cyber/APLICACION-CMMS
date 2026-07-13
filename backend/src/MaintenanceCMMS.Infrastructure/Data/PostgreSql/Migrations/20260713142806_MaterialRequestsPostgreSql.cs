using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class MaterialRequestsPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "solicitudes_repuestos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_solicitud = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    estado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tipo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    origen = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: true),
                    solicitante_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    solicitado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    descripcion_tecnica = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    unidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    foto_referencia = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    codigo_tarea = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    decision_stock = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aprobador_mantenimiento_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    aprobado_mantenimiento_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    aprobador_bodega_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    aprobado_bodega_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    rechazado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    rechazado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recibido_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    recibido_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    convertido_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    convertido_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cerrado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitudes_repuestos", x => x.id);
                    table.ForeignKey(
                        name: "FK_solicitudes_repuestos_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitudes_repuestos_bodegas_bodega_id",
                        column: x => x.bodega_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitudes_repuestos_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitudes_repuestos_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "solicitud_repuesto_historial",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitud_repuesto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_anterior = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    estado_nuevo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitud_repuesto_historial", x => x.id);
                    table.ForeignKey(
                        name: "FK_solicitud_repuesto_historial_solicitudes_repuestos_solicitu~",
                        column: x => x.solicitud_repuesto_id,
                        principalTable: "solicitudes_repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "solicitud_repuesto_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitud_repuesto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reserva_id = table.Column<Guid>(type: "uuid", nullable: true),
                    repuesto_maestro_codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    cantidad_solicitada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_aprobada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_reservada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_entregada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_devuelta = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    unidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    movimiento_entrega_numero = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitud_repuesto_items", x => x.id);
                    table.CheckConstraint("ck_solicitud_repuesto_items_cantidades", "cantidad_solicitada > 0 AND cantidad_aprobada >= 0 AND cantidad_reservada >= 0 AND cantidad_entregada >= 0 AND cantidad_devuelta >= 0");
                    table.ForeignKey(
                        name: "FK_solicitud_repuesto_items_repuestos_repuesto_id",
                        column: x => x.repuesto_id,
                        principalTable: "repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitud_repuesto_items_reservas_stock_reserva_id",
                        column: x => x.reserva_id,
                        principalTable: "reservas_stock",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitud_repuesto_items_solicitudes_repuestos_solicitud_re~",
                        column: x => x.solicitud_repuesto_id,
                        principalTable: "solicitudes_repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_repuesto_historial_solicitud_repuesto_id_fecha_utc",
                table: "solicitud_repuesto_historial",
                columns: new[] { "solicitud_repuesto_id", "fecha_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_repuesto_items_repuesto_id",
                table: "solicitud_repuesto_items",
                column: "repuesto_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_repuesto_items_reserva_id",
                table: "solicitud_repuesto_items",
                column: "reserva_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_repuesto_items_solicitud_repuesto_id",
                table: "solicitud_repuesto_items",
                column: "solicitud_repuesto_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_repuestos_activo_id",
                table: "solicitudes_repuestos",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_repuestos_bodega_id",
                table: "solicitudes_repuestos",
                column: "bodega_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_repuestos_faena_id_estado",
                table: "solicitudes_repuestos",
                columns: new[] { "faena_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_repuestos_numero_solicitud",
                table: "solicitudes_repuestos",
                column: "numero_solicitud",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_repuestos_orden_trabajo_id",
                table: "solicitudes_repuestos",
                column: "orden_trabajo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solicitud_repuesto_historial");

            migrationBuilder.DropTable(
                name: "solicitud_repuesto_items");

            migrationBuilder.DropTable(
                name: "solicitudes_repuestos");
        }
    }
}

