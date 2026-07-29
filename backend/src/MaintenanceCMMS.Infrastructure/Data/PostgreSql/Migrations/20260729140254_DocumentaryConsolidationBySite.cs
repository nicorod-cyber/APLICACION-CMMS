using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class DocumentaryConsolidationBySite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matrices_requisitos_documentales_codigo_numero_version",
                table: "matrices_requisitos_documentales");

            migrationBuilder.DropIndex(
                name: "IX_matrices_requisitos_documentales_tipo_activo_id_familia_equ~",
                table: "matrices_requisitos_documentales");

            migrationBuilder.AddColumn<string>(
                name: "extensiones_permitidas",
                table: "tipos_documentales",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plantilla_carpeta",
                table: "tipos_documentales",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "storage_destination_key",
                table: "tipos_documentales",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "tamano_maximo_bytes",
                table: "tipos_documentales",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "faena_id",
                table: "matrices_requisitos_documentales",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE matrices_requisitos_documentales SET estado = 'BORRADOR', updated_at_utc = NOW() WHERE faena_id IS NULL AND estado = 'VIGENTE';");

            migrationBuilder.AddColumn<Guid>(
                name: "activo_requisito_activo_id",
                table: "documentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "matriz_requisito_id",
                table: "documentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "requisito_matriz_item_id",
                table: "documentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "bloquea_disponibilidad_snapshot",
                table: "detalles_ot_documental",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "critico_snapshot",
                table: "detalles_ot_documental",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "dias_anticipacion_snapshot",
                table: "detalles_ot_documental",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "faena_codigo_snapshot",
                table: "detalles_ot_documental",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "obligatorio_snapshot",
                table: "detalles_ot_documental",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "requiere_fecha_vencimiento_snapshot",
                table: "detalles_ot_documental",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reutilizable_entre_faenas_snapshot",
                table: "detalles_ot_documental",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "tipo_documental_codigo_snapshot",
                table: "detalles_ot_documental",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tipo_documental_nombre_snapshot",
                table: "detalles_ot_documental",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "orden_presentacion",
                table: "detalles_matriz_requisitos_documentales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "reutilizable_entre_faenas",
                table: "detalles_matriz_requisitos_documentales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipos_documentales_tamano_maximo",
                table: "tipos_documentales",
                sql: "tamano_maximo_bytes IS NULL OR tamano_maximo_bytes > 0");

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_codigo_numero_ver~",
                table: "matrices_requisitos_documentales",
                columns: new[] { "faena_id", "codigo", "numero_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_tipo_activo_id",
                table: "matrices_requisitos_documentales",
                columns: new[] { "faena_id", "tipo_activo_id" },
                unique: true,
                filter: "faena_id IS NOT NULL AND familia_equipo_id IS NULL AND estado = 'VIGENTE' AND vigencia_hasta IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_tipo_activo_id_f~1",
                table: "matrices_requisitos_documentales",
                columns: new[] { "faena_id", "tipo_activo_id", "familia_equipo_id", "vigencia_desde" });

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_tipo_activo_id_fa~",
                table: "matrices_requisitos_documentales",
                columns: new[] { "faena_id", "tipo_activo_id", "familia_equipo_id" },
                unique: true,
                filter: "faena_id IS NOT NULL AND familia_equipo_id IS NOT NULL AND estado = 'VIGENTE' AND vigencia_hasta IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_tipo_activo_id",
                table: "matrices_requisitos_documentales",
                column: "tipo_activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_activo_requisito_activo_id_requisito_matriz_item~",
                table: "documentos",
                columns: new[] { "activo_requisito_activo_id", "requisito_matriz_item_id" },
                unique: true,
                filter: "activo_requisito_activo_id IS NOT NULL AND requisito_matriz_item_id IS NOT NULL AND NOT anulado AND NOT historico");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_matriz_requisito_id",
                table: "documentos",
                column: "matriz_requisito_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_requisito_matriz_item_id",
                table: "documentos",
                column: "requisito_matriz_item_id");

            migrationBuilder.AddForeignKey(
                name: "FK_documentos_activos_activo_requisito_activo_id",
                table: "documentos",
                column: "activo_requisito_activo_id",
                principalTable: "activos",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_documentos_detalles_matriz_requisitos_documentales_requisit~",
                table: "documentos",
                column: "requisito_matriz_item_id",
                principalTable: "detalles_matriz_requisitos_documentales",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_documentos_matrices_requisitos_documentales_matriz_requisit~",
                table: "documentos",
                column: "matriz_requisito_id",
                principalTable: "matrices_requisitos_documentales",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_matrices_requisitos_documentales_faenas_faena_id",
                table: "matrices_requisitos_documentales",
                column: "faena_id",
                principalTable: "faenas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documentos_activos_activo_requisito_activo_id",
                table: "documentos");

            migrationBuilder.DropForeignKey(
                name: "FK_documentos_detalles_matriz_requisitos_documentales_requisit~",
                table: "documentos");

            migrationBuilder.DropForeignKey(
                name: "FK_documentos_matrices_requisitos_documentales_matriz_requisit~",
                table: "documentos");

            migrationBuilder.DropForeignKey(
                name: "FK_matrices_requisitos_documentales_faenas_faena_id",
                table: "matrices_requisitos_documentales");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tipos_documentales_tamano_maximo",
                table: "tipos_documentales");

            migrationBuilder.DropIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_codigo_numero_ver~",
                table: "matrices_requisitos_documentales");

            migrationBuilder.DropIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_tipo_activo_id",
                table: "matrices_requisitos_documentales");

            migrationBuilder.DropIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_tipo_activo_id_f~1",
                table: "matrices_requisitos_documentales");

            migrationBuilder.DropIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_tipo_activo_id_fa~",
                table: "matrices_requisitos_documentales");

            migrationBuilder.DropIndex(
                name: "IX_matrices_requisitos_documentales_tipo_activo_id",
                table: "matrices_requisitos_documentales");

            migrationBuilder.DropIndex(
                name: "IX_documentos_activo_requisito_activo_id_requisito_matriz_item~",
                table: "documentos");

            migrationBuilder.DropIndex(
                name: "IX_documentos_matriz_requisito_id",
                table: "documentos");

            migrationBuilder.DropIndex(
                name: "IX_documentos_requisito_matriz_item_id",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "extensiones_permitidas",
                table: "tipos_documentales");

            migrationBuilder.DropColumn(
                name: "plantilla_carpeta",
                table: "tipos_documentales");

            migrationBuilder.DropColumn(
                name: "storage_destination_key",
                table: "tipos_documentales");

            migrationBuilder.DropColumn(
                name: "tamano_maximo_bytes",
                table: "tipos_documentales");

            migrationBuilder.DropColumn(
                name: "faena_id",
                table: "matrices_requisitos_documentales");

            migrationBuilder.DropColumn(
                name: "activo_requisito_activo_id",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "matriz_requisito_id",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "requisito_matriz_item_id",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "bloquea_disponibilidad_snapshot",
                table: "detalles_ot_documental");

            migrationBuilder.DropColumn(
                name: "critico_snapshot",
                table: "detalles_ot_documental");

            migrationBuilder.DropColumn(
                name: "dias_anticipacion_snapshot",
                table: "detalles_ot_documental");

            migrationBuilder.DropColumn(
                name: "faena_codigo_snapshot",
                table: "detalles_ot_documental");

            migrationBuilder.DropColumn(
                name: "obligatorio_snapshot",
                table: "detalles_ot_documental");

            migrationBuilder.DropColumn(
                name: "requiere_fecha_vencimiento_snapshot",
                table: "detalles_ot_documental");

            migrationBuilder.DropColumn(
                name: "reutilizable_entre_faenas_snapshot",
                table: "detalles_ot_documental");

            migrationBuilder.DropColumn(
                name: "tipo_documental_codigo_snapshot",
                table: "detalles_ot_documental");

            migrationBuilder.DropColumn(
                name: "tipo_documental_nombre_snapshot",
                table: "detalles_ot_documental");

            migrationBuilder.DropColumn(
                name: "orden_presentacion",
                table: "detalles_matriz_requisitos_documentales");

            migrationBuilder.DropColumn(
                name: "reutilizable_entre_faenas",
                table: "detalles_matriz_requisitos_documentales");

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_codigo_numero_version",
                table: "matrices_requisitos_documentales",
                columns: new[] { "codigo", "numero_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_tipo_activo_id_familia_equ~",
                table: "matrices_requisitos_documentales",
                columns: new[] { "tipo_activo_id", "familia_equipo_id", "vigencia_desde" });
        }
    }
}
