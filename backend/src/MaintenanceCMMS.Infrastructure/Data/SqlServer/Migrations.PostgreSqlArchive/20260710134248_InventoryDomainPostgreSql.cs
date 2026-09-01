using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InventoryDomainPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The extension and all non-inventory tables are owned by earlier migrations.
            // This migration must remain strictly incremental.
            migrationBuilder.CreateSequence(name: "spare_part_number_seq");
            migrationBuilder.CreateSequence(name: "stock_movement_number_seq");
            migrationBuilder.CreateSequence(name: "stock_reservation_number_seq");
            migrationBuilder.CreateSequence(name: "stock_transfer_number_seq");

            migrationBuilder.CreateTable(
                name: "catalogos_inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogos_inventario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "repuestos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    codigo_sap = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    codigo_proveedor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descripcion_tecnica = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fabricante = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    modelo_referencia = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    critico = table.Column<bool>(type: "boolean", nullable: false),
                    stock_minimo = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    stock_maximo = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    punto_reposicion = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    lead_time_dias = table.Column<int>(type: "integer", nullable: false),
                    costo_unitario_promedio = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    proveedor_preferente = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    reemplazo_codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repuestos", x => x.id);
                    table.CheckConstraint("ck_repuestos_stocks", "stock_minimo >= 0 AND stock_maximo >= 0 AND punto_reposicion >= 0");
                    table.ForeignKey(
                        name: "FK_repuestos_catalogos_inventario_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "catalogos_inventario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_repuestos_catalogos_inventario_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "catalogos_inventario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bodegas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ubicacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    responsable_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    permite_stock_negativo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bodegas", x => x.id);
                    table.ForeignKey(
                        name: "FK_bodegas_catalogos_inventario_tipo_id",
                        column: x => x.tipo_id,
                        principalTable: "catalogos_inventario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bodegas_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transferencias_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    bodega_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_transito_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    solicitado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    solicitado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    recibido_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    recibido_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    motivo_recepcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    motivo_anulacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferencias_stock", x => x.id);
                    table.CheckConstraint("ck_transferencias_stock_bodegas", "bodega_origen_id <> bodega_destino_id AND cantidad > 0");
                    table.ForeignKey(
                        name: "FK_transferencias_stock_bodegas_bodega_destino_id",
                        column: x => x.bodega_destino_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transferencias_stock_bodegas_bodega_origen_id",
                        column: x => x.bodega_origen_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transferencias_stock_bodegas_bodega_transito_id",
                        column: x => x.bodega_transito_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transferencias_stock_repuestos_repuesto_id",
                        column: x => x.repuesto_id,
                        principalTable: "repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ubicaciones_bodega",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pasillo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    estante = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    nivel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    posicion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ubicaciones_bodega", x => x.id);
                    table.ForeignKey(
                        name: "FK_ubicaciones_bodega_bodegas_bodega_id",
                        column: x => x.bodega_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_bodega",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ubicacion_bodega_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cantidad_fisica = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_reservada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    stock_minimo_especifico = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_bodega", x => x.id);
                    table.CheckConstraint("ck_stock_bodega_saldos", "cantidad_fisica >= 0 AND cantidad_reservada >= 0 AND cantidad_reservada <= cantidad_fisica");
                    table.ForeignKey(
                        name: "FK_stock_bodega_bodegas_bodega_id",
                        column: x => x.bodega_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_bodega_repuestos_repuesto_id",
                        column: x => x.repuesto_id,
                        principalTable: "repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_bodega_ubicaciones_bodega_ubicacion_bodega_id",
                        column: x => x.ubicacion_bodega_id,
                        principalTable: "ubicaciones_bodega",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reservas_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_solicitada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_reservada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_entregada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_liberada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden_trabajo_numero = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    solicitante = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    motivo_anulacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entregado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    liberado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservas_stock", x => x.id);
                    table.CheckConstraint("ck_reservas_stock_cantidades", "cantidad_solicitada > 0 AND cantidad_reservada >= 0 AND cantidad_entregada >= 0 AND cantidad_liberada >= 0 AND cantidad_entregada + cantidad_liberada <= cantidad_reservada");
                    table.ForeignKey(
                        name: "FK_reservas_stock_bodegas_bodega_id",
                        column: x => x.bodega_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservas_stock_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservas_stock_repuestos_repuesto_id",
                        column: x => x.repuesto_id,
                        principalTable: "repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimientos_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_movimiento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tipo_movimiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    bodega_origen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bodega_destino_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reserva_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_referencia = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    referencia_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fisico_anterior = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    fisico_nuevo = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    reservado_anterior = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    reservado_nuevo = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    anulado = table.Column<bool>(type: "boolean", nullable: false),
                    movimiento_reverso_de_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientos_stock", x => x.id);
                    table.CheckConstraint("ck_movimientos_stock_cantidad", "cantidad > 0");
                    table.ForeignKey(
                        name: "FK_movimientos_stock_bodegas_bodega_destino_id",
                        column: x => x.bodega_destino_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_stock_bodegas_bodega_origen_id",
                        column: x => x.bodega_origen_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_stock_catalogos_inventario_tipo_movimiento_id",
                        column: x => x.tipo_movimiento_id,
                        principalTable: "catalogos_inventario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_stock_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_stock_repuestos_repuesto_id",
                        column: x => x.repuesto_id,
                        principalTable: "repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_stock_reservas_stock_reserva_id",
                        column: x => x.reserva_id,
                        principalTable: "reservas_stock",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_stock_transferencias_stock_transferencia_id",
                        column: x => x.transferencia_id,
                        principalTable: "transferencias_stock",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bodegas_codigo",
                table: "bodegas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bodegas_faena_id",
                table: "bodegas",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_bodegas_tipo_id",
                table: "bodegas",
                column: "tipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_catalogos_inventario_categoria_codigo",
                table: "catalogos_inventario",
                columns: new[] { "categoria", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_stock_bodega_destino_id",
                table: "movimientos_stock",
                column: "bodega_destino_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_stock_bodega_origen_id",
                table: "movimientos_stock",
                column: "bodega_origen_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_stock_numero_movimiento",
                table: "movimientos_stock",
                column: "numero_movimiento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_stock_orden_trabajo_id",
                table: "movimientos_stock",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_stock_repuesto_id_fecha_utc",
                table: "movimientos_stock",
                columns: new[] { "repuesto_id", "fecha_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_stock_reserva_id",
                table: "movimientos_stock",
                column: "reserva_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_stock_tipo_movimiento_id",
                table: "movimientos_stock",
                column: "tipo_movimiento_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_stock_transferencia_id",
                table: "movimientos_stock",
                column: "transferencia_id");

            migrationBuilder.CreateIndex(
                name: "IX_repuestos_categoria_id",
                table: "repuestos",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_repuestos_codigo",
                table: "repuestos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repuestos_codigo_sap",
                table: "repuestos",
                column: "codigo_sap",
                unique: true,
                filter: "codigo_sap IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_repuestos_unidad_id",
                table: "repuestos",
                column: "unidad_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservas_stock_bodega_id",
                table: "reservas_stock",
                column: "bodega_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservas_stock_codigo",
                table: "reservas_stock",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservas_stock_orden_trabajo_id",
                table: "reservas_stock",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservas_stock_repuesto_id",
                table: "reservas_stock",
                column: "repuesto_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_bodega_bodega_id",
                table: "stock_bodega",
                column: "bodega_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_bodega_repuesto_id_bodega_id_ubicacion_bodega_id",
                table: "stock_bodega",
                columns: new[] { "repuesto_id", "bodega_id", "ubicacion_bodega_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_bodega_ubicacion_bodega_id",
                table: "stock_bodega",
                column: "ubicacion_bodega_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_stock_bodega_destino_id",
                table: "transferencias_stock",
                column: "bodega_destino_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_stock_bodega_origen_id",
                table: "transferencias_stock",
                column: "bodega_origen_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_stock_bodega_transito_id",
                table: "transferencias_stock",
                column: "bodega_transito_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_stock_codigo",
                table: "transferencias_stock",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_stock_repuesto_id",
                table: "transferencias_stock",
                column: "repuesto_id");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_bodega_bodega_id_codigo",
                table: "ubicaciones_bodega",
                columns: new[] { "bodega_id", "codigo" },
                unique: true);

        }
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "movimientos_stock");
            migrationBuilder.DropTable(name: "reservas_stock");
            migrationBuilder.DropTable(name: "stock_bodega");
            migrationBuilder.DropTable(name: "transferencias_stock");
            migrationBuilder.DropTable(name: "ubicaciones_bodega");
            migrationBuilder.DropTable(name: "bodegas");
            migrationBuilder.DropTable(name: "repuestos");
            migrationBuilder.DropTable(name: "catalogos_inventario");
            migrationBuilder.DropSequence(name: "spare_part_number_seq");
            migrationBuilder.DropSequence(name: "stock_movement_number_seq");
            migrationBuilder.DropSequence(name: "stock_reservation_number_seq");
            migrationBuilder.DropSequence(name: "stock_transfer_number_seq");
        }
    }
}
