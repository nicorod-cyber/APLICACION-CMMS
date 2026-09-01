using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations;

public partial class GlobalWorkshopsAndPhysicalAssetLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "comuna", table: "talleres", type: "character varying(160)", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "supervisor_usuario_id", table: "talleres", type: "uuid", nullable: true);

        migrationBuilder.CreateTable(
            name: "vigencias_ubicacion_fisica_activo",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false), activo_id = table.Column<Guid>(type: "uuid", nullable: false), tipo_ubicacion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false), faena_id = table.Column<Guid>(type: "uuid", nullable: true), taller_id = table.Column<Guid>(type: "uuid", nullable: true), vigencia_desde_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false), vigencia_hasta_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true), motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true), registrado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false), orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true), unidad_operativa_id = table.Column<Guid>(type: "uuid", nullable: true), observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true), created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false), updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true), xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_vigencias_ubicacion_fisica_activo", x => x.id);
                table.CheckConstraint("ck_vigencias_ubicacion_fisica_tipo", "(tipo_ubicacion = 'FAENA' AND faena_id IS NOT NULL AND taller_id IS NULL) OR (tipo_ubicacion = 'TALLER' AND taller_id IS NOT NULL AND faena_id IS NULL)");
                table.CheckConstraint("ck_vigencias_ubicacion_fisica_fechas", "vigencia_hasta_utc IS NULL OR vigencia_hasta_utc > vigencia_desde_utc");
                table.ForeignKey("FK_vigencias_ubicacion_fisica_activo_activos_activo_id", x => x.activo_id, "activos", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_vigencias_ubicacion_fisica_activo_faenas_faena_id", x => x.faena_id, "faenas", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_vigencias_ubicacion_fisica_activo_talleres_taller_id", x => x.taller_id, "talleres", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_vigencias_ubicacion_fisica_activo_ordenes_trabajo_sql_orden_trabajo_id", x => x.orden_trabajo_id, "ordenes_trabajo_sql", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_vigencias_ubicacion_fisica_activo_unidades_operativas_unidad_operativa_id", x => x.unidad_operativa_id, "unidades_operativas", "id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.Sql(@"INSERT INTO vigencias_ubicacion_fisica_activo (id, activo_id, tipo_ubicacion, faena_id, vigencia_desde_utc, registrado_por_usuario_id, created_at_utc) SELECT gen_random_uuid(), a.id, 'FAENA', a.faena_id, COALESCE(a.created_at_utc, CURRENT_TIMESTAMP), 'MIGRACION', CURRENT_TIMESTAMP FROM activos a WHERE a.faena_id IS NOT NULL;");
        migrationBuilder.Sql(@"DO $$ BEGIN IF EXISTS (SELECT 1 FROM activos a WHERE NOT EXISTS (SELECT 1 FROM vigencias_ubicacion_fisica_activo v WHERE v.activo_id = a.id AND v.vigencia_hasta_utc IS NULL)) THEN RAISE EXCEPTION 'No se pudo inicializar ubicacion fisica para activos sin faena asignada.'; END IF; END $$;");
        migrationBuilder.CreateIndex(name: "IX_vigencias_ubicacion_fisica_activo_activo_id", table: "vigencias_ubicacion_fisica_activo", column: "activo_id", unique: true, filter: "vigencia_hasta_utc IS NULL");
        migrationBuilder.CreateIndex(name: "IX_vigencias_ubicacion_fisica_activo_activo_id_vigencia_desde_utc", table: "vigencias_ubicacion_fisica_activo", columns: new[] { "activo_id", "vigencia_desde_utc" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_vigencias_ubicacion_fisica_activo_faena_id", table: "vigencias_ubicacion_fisica_activo", column: "faena_id");
        migrationBuilder.CreateIndex(name: "IX_vigencias_ubicacion_fisica_activo_taller_id", table: "vigencias_ubicacion_fisica_activo", column: "taller_id");
        migrationBuilder.CreateIndex(name: "IX_vigencias_ubicacion_fisica_activo_orden_trabajo_id", table: "vigencias_ubicacion_fisica_activo", column: "orden_trabajo_id");
        migrationBuilder.CreateIndex(name: "IX_vigencias_ubicacion_fisica_activo_unidad_operativa_id", table: "vigencias_ubicacion_fisica_activo", column: "unidad_operativa_id");

        migrationBuilder.DropForeignKey(name: "FK_talleres_faenas_faena_id", table: "talleres");
        migrationBuilder.DropIndex(name: "IX_talleres_faena_id", table: "talleres");
        migrationBuilder.DropCheckConstraint(name: "ck_talleres_capacidad_hh", table: "talleres");
        migrationBuilder.DropColumn(name: "capacidad_diaria_hh", table: "talleres"); migrationBuilder.DropColumn(name: "especialidad", table: "talleres"); migrationBuilder.DropColumn(name: "horario", table: "talleres"); migrationBuilder.DropColumn(name: "faena_id", table: "talleres");
        migrationBuilder.CreateIndex(name: "IX_talleres_supervisor_usuario_id", table: "talleres", column: "supervisor_usuario_id");
        migrationBuilder.AddForeignKey(name: "FK_talleres_usuarios_supervisor_usuario_id", table: "talleres", column: "supervisor_usuario_id", principalTable: "usuarios", principalColumn: "id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_talleres_usuarios_supervisor_usuario_id", table: "talleres"); migrationBuilder.DropIndex(name: "IX_talleres_supervisor_usuario_id", table: "talleres"); migrationBuilder.DropTable(name: "vigencias_ubicacion_fisica_activo"); migrationBuilder.DropColumn(name: "comuna", table: "talleres"); migrationBuilder.DropColumn(name: "supervisor_usuario_id", table: "talleres");
        migrationBuilder.AddColumn<Guid>(name: "faena_id", table: "talleres", type: "uuid", nullable: false, defaultValue: Guid.Empty); migrationBuilder.AddColumn<decimal>(name: "capacidad_diaria_hh", table: "talleres", type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m); migrationBuilder.AddColumn<string>(name: "horario", table: "talleres", type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""); migrationBuilder.AddColumn<string>(name: "especialidad", table: "talleres", type: "character varying(240)", maxLength: 240, nullable: false, defaultValue: "");
        migrationBuilder.CreateIndex(name: "IX_talleres_faena_id", table: "talleres", column: "faena_id"); migrationBuilder.AddCheckConstraint(name: "ck_talleres_capacidad_hh", table: "talleres", sql: "capacidad_diaria_hh >= 0"); migrationBuilder.AddForeignKey(name: "FK_talleres_faenas_faena_id", table: "talleres", column: "faena_id", principalTable: "faenas", principalColumn: "id", onDelete: ReferentialAction.Restrict);
    }
}