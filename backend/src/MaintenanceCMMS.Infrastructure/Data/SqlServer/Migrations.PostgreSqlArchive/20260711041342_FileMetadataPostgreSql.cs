using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class FileMetadataPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "activo_codigo",
                table: "archivos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "eliminado",
                table: "archivos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "eliminado_at_utc",
                table: "archivos",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eliminado_por_usuario_id",
                table: "archivos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entidad_id",
                table: "archivos",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "extension",
                table: "archivos",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "faena_codigo",
                table: "archivos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modo_almacenamiento",
                table: "archivos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "modulo",
                table: "archivos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "nombre_almacenado",
                table: "archivos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "numero_ot",
                table: "archivos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proposito",
                table: "archivos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tipo_entidad",
                table: "archivos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ubicacion_fisica",
                table: "archivos",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version_archivo",
                table: "archivos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE archivos
                SET nombre_almacenado = CASE WHEN nombre_almacenado = '' THEN COALESCE(NULLIF(nombre, ''), 'archivo') ELSE nombre_almacenado END,
                    extension = CASE
                        WHEN extension <> '' THEN extension
                        WHEN position('.' IN nombre) > 0 THEN lower(reverse(split_part(reverse(nombre), '.', 1)))
                        ELSE ''
                    END,
                    proveedor = CASE WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor ELSE 'ManualLink' END,
                    modo_almacenamiento = CASE
                        WHEN modo_almacenamiento IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN modo_almacenamiento
                        WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor
                        ELSE 'ManualLink'
                    END,
                    proposito = CASE WHEN proposito <> '' THEN proposito ELSE 'Document' END,
                    modulo = CASE WHEN modulo <> '' THEN modulo ELSE 'Legacy' END,
                    tipo_entidad = CASE WHEN tipo_entidad <> '' THEN tipo_entidad ELSE 'Legacy' END,
                    entidad_id = CASE WHEN entidad_id <> '' THEN entidad_id ELSE file_key END,
                    version_archivo = CASE WHEN version_archivo < 1 THEN 1 ELSE version_archivo END,
                    estado = CASE
                        WHEN estado IN ('Stored', 'ManualLink', 'PendingManualLink', 'GraphApiReady', 'InvalidPath', 'Deleted') THEN estado
                        ELSE 'Stored'
                    END;
                """);
            migrationBuilder.CreateIndex(
                name: "IX_archivos_checksum",
                table: "archivos",
                column: "checksum");

            migrationBuilder.Sql("""
                UPDATE archivos
                SET nombre_almacenado = CASE WHEN nombre_almacenado = '' THEN COALESCE(NULLIF(nombre, ''), 'archivo') ELSE nombre_almacenado END,
                    extension = CASE
                        WHEN extension <> '' THEN extension
                        WHEN position('.' IN nombre) > 0 THEN lower(reverse(split_part(reverse(nombre), '.', 1)))
                        ELSE ''
                    END,
                    proveedor = CASE WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor ELSE 'ManualLink' END,
                    modo_almacenamiento = CASE
                        WHEN modo_almacenamiento IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN modo_almacenamiento
                        WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor
                        ELSE 'ManualLink'
                    END,
                    proposito = CASE WHEN proposito <> '' THEN proposito ELSE 'Document' END,
                    modulo = CASE WHEN modulo <> '' THEN modulo ELSE 'Legacy' END,
                    tipo_entidad = CASE WHEN tipo_entidad <> '' THEN tipo_entidad ELSE 'Legacy' END,
                    entidad_id = CASE WHEN entidad_id <> '' THEN entidad_id ELSE file_key END,
                    version_archivo = CASE WHEN version_archivo < 1 THEN 1 ELSE version_archivo END,
                    estado = CASE
                        WHEN estado IN ('Stored', 'ManualLink', 'PendingManualLink', 'GraphApiReady', 'InvalidPath', 'Deleted') THEN estado
                        ELSE 'Stored'
                    END;
                """);
            migrationBuilder.CreateIndex(
                name: "IX_archivos_created_at_utc",
                table: "archivos",
                column: "created_at_utc");

            migrationBuilder.Sql("""
                UPDATE archivos
                SET nombre_almacenado = CASE WHEN nombre_almacenado = '' THEN COALESCE(NULLIF(nombre, ''), 'archivo') ELSE nombre_almacenado END,
                    extension = CASE
                        WHEN extension <> '' THEN extension
                        WHEN position('.' IN nombre) > 0 THEN lower(reverse(split_part(reverse(nombre), '.', 1)))
                        ELSE ''
                    END,
                    proveedor = CASE WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor ELSE 'ManualLink' END,
                    modo_almacenamiento = CASE
                        WHEN modo_almacenamiento IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN modo_almacenamiento
                        WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor
                        ELSE 'ManualLink'
                    END,
                    proposito = CASE WHEN proposito <> '' THEN proposito ELSE 'Document' END,
                    modulo = CASE WHEN modulo <> '' THEN modulo ELSE 'Legacy' END,
                    tipo_entidad = CASE WHEN tipo_entidad <> '' THEN tipo_entidad ELSE 'Legacy' END,
                    entidad_id = CASE WHEN entidad_id <> '' THEN entidad_id ELSE file_key END,
                    version_archivo = CASE WHEN version_archivo < 1 THEN 1 ELSE version_archivo END,
                    estado = CASE
                        WHEN estado IN ('Stored', 'ManualLink', 'PendingManualLink', 'GraphApiReady', 'InvalidPath', 'Deleted') THEN estado
                        ELSE 'Stored'
                    END;
                """);
            migrationBuilder.CreateIndex(
                name: "IX_archivos_proveedor",
                table: "archivos",
                column: "proveedor");

            migrationBuilder.Sql("""
                UPDATE archivos
                SET nombre_almacenado = CASE WHEN nombre_almacenado = '' THEN COALESCE(NULLIF(nombre, ''), 'archivo') ELSE nombre_almacenado END,
                    extension = CASE
                        WHEN extension <> '' THEN extension
                        WHEN position('.' IN nombre) > 0 THEN lower(reverse(split_part(reverse(nombre), '.', 1)))
                        ELSE ''
                    END,
                    proveedor = CASE WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor ELSE 'ManualLink' END,
                    modo_almacenamiento = CASE
                        WHEN modo_almacenamiento IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN modo_almacenamiento
                        WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor
                        ELSE 'ManualLink'
                    END,
                    proposito = CASE WHEN proposito <> '' THEN proposito ELSE 'Document' END,
                    modulo = CASE WHEN modulo <> '' THEN modulo ELSE 'Legacy' END,
                    tipo_entidad = CASE WHEN tipo_entidad <> '' THEN tipo_entidad ELSE 'Legacy' END,
                    entidad_id = CASE WHEN entidad_id <> '' THEN entidad_id ELSE file_key END,
                    version_archivo = CASE WHEN version_archivo < 1 THEN 1 ELSE version_archivo END,
                    estado = CASE
                        WHEN estado IN ('Stored', 'ManualLink', 'PendingManualLink', 'GraphApiReady', 'InvalidPath', 'Deleted') THEN estado
                        ELSE 'Stored'
                    END;
                """);
            migrationBuilder.CreateIndex(
                name: "IX_archivos_tipo_entidad_entidad_id_eliminado",
                table: "archivos",
                columns: new[] { "tipo_entidad", "entidad_id", "eliminado" });

            migrationBuilder.Sql("""
                UPDATE archivos
                SET nombre_almacenado = CASE WHEN nombre_almacenado = '' THEN COALESCE(NULLIF(nombre, ''), 'archivo') ELSE nombre_almacenado END,
                    extension = CASE
                        WHEN extension <> '' THEN extension
                        WHEN position('.' IN nombre) > 0 THEN lower(reverse(split_part(reverse(nombre), '.', 1)))
                        ELSE ''
                    END,
                    proveedor = CASE WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor ELSE 'ManualLink' END,
                    modo_almacenamiento = CASE
                        WHEN modo_almacenamiento IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN modo_almacenamiento
                        WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor
                        ELSE 'ManualLink'
                    END,
                    proposito = CASE WHEN proposito <> '' THEN proposito ELSE 'Document' END,
                    modulo = CASE WHEN modulo <> '' THEN modulo ELSE 'Legacy' END,
                    tipo_entidad = CASE WHEN tipo_entidad <> '' THEN tipo_entidad ELSE 'Legacy' END,
                    entidad_id = CASE WHEN entidad_id <> '' THEN entidad_id ELSE file_key END,
                    version_archivo = CASE WHEN version_archivo < 1 THEN 1 ELSE version_archivo END,
                    estado = CASE
                        WHEN estado IN ('Stored', 'ManualLink', 'PendingManualLink', 'GraphApiReady', 'InvalidPath', 'Deleted') THEN estado
                        ELSE 'Stored'
                    END;
                """);
            migrationBuilder.CreateIndex(
                name: "IX_archivos_uri_logica",
                table: "archivos",
                column: "uri_logica");

            migrationBuilder.AddCheckConstraint(
                name: "ck_archivos_estado",
                table: "archivos",
                sql: "estado IN ('Stored','ManualLink','PendingManualLink','GraphApiReady','InvalidPath','Deleted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_archivos_proveedor",
                table: "archivos",
                sql: "proveedor IN ('ManualLink','LocalSimulation','GraphApiReady')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_archivos_tamano_no_negativo",
                table: "archivos",
                sql: "tamano_bytes IS NULL OR tamano_bytes >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_archivos_checksum",
                table: "archivos");

            migrationBuilder.DropIndex(
                name: "IX_archivos_created_at_utc",
                table: "archivos");

            migrationBuilder.DropIndex(
                name: "IX_archivos_proveedor",
                table: "archivos");

            migrationBuilder.DropIndex(
                name: "IX_archivos_tipo_entidad_entidad_id_eliminado",
                table: "archivos");

            migrationBuilder.DropIndex(
                name: "IX_archivos_uri_logica",
                table: "archivos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_archivos_estado",
                table: "archivos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_archivos_proveedor",
                table: "archivos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_archivos_tamano_no_negativo",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "activo_codigo",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "eliminado",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "eliminado_at_utc",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "eliminado_por_usuario_id",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "entidad_id",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "extension",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "faena_codigo",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "modo_almacenamiento",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "modulo",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "nombre_almacenado",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "numero_ot",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "proposito",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "tipo_entidad",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "ubicacion_fisica",
                table: "archivos");

            migrationBuilder.DropColumn(
                name: "version_archivo",
                table: "archivos");
        }
    }
}
