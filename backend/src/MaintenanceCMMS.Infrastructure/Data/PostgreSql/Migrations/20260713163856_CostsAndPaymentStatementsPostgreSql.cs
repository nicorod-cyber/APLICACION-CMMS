using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CostsAndPaymentStatementsPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "costos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    categoria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    moneda = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    FaenaId = table.Column<Guid>(type: "uuid", nullable: true),
                    SparePartId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    contrato_codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    proveedor_rut = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    cantidad = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    costo_unitario = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    documento_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_costos", x => x.id);
                    table.ForeignKey(
                        name: "FK_costos_activos_AssetId",
                        column: x => x.AssetId,
                        principalTable: "activos",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_costos_faenas_FaenaId",
                        column: x => x.FaenaId,
                        principalTable: "faenas",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_costos_movimientos_stock_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "movimientos_stock",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_costos_ordenes_trabajo_sql_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_costos_repuestos_SparePartId",
                        column: x => x.SparePartId,
                        principalTable: "repuestos",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "estados_pago",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    proveedor_rut = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContractCode = table.Column<string>(type: "text", nullable: true),
                    FaenaId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true),
                    StatusChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectReason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estados_pago", x => x.id);
                    table.ForeignKey(
                        name: "FK_estados_pago_faenas_FaenaId",
                        column: x => x.FaenaId,
                        principalTable: "faenas",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tarifas_hh",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    especialidad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tarifa_hora = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarifas_hh", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_costos_AssetId",
                table: "costos",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_costos_FaenaId_fecha_utc",
                table: "costos",
                columns: new[] { "FaenaId", "fecha_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_costos_numero",
                table: "costos",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_costos_SparePartId",
                table: "costos",
                column: "SparePartId");

            migrationBuilder.CreateIndex(
                name: "IX_costos_StockMovementId",
                table: "costos",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_costos_WorkOrderId",
                table: "costos",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_estados_pago_FaenaId",
                table: "estados_pago",
                column: "FaenaId");

            migrationBuilder.CreateIndex(
                name: "IX_estados_pago_numero",
                table: "estados_pago",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_estados_pago_proveedor_rut_estado",
                table: "estados_pago",
                columns: new[] { "proveedor_rut", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_tarifas_hh_codigo",
                table: "tarifas_hh",
                column: "codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "costos");

            migrationBuilder.DropTable(
                name: "estados_pago");

            migrationBuilder.DropTable(
                name: "tarifas_hh");
        }
    }
}
