using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations;

[DbContext(typeof(CmmsDbContext))]
[Migration("202607090002_DocumentDomainPostgreSql")]
public partial class DocumentDomainPostgreSql : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tipos_documentales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                aplica_a = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                critico = table.Column<bool>(type: "boolean", nullable: false),
                bloquea_disponibilidad = table.Column<bool>(type: "boolean", nullable: false),
                dias_alerta = table.Column<int>(type: "integer", nullable: false),
                roles_responsables = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                requiere_pdf_alerta = table.Column<bool>(type: "boolean", nullable: false),
                plantilla_html_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                activo = table.Column<bool>(type: "boolean", nullable: false),
                created_by_user_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                updated_by_user_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tipos_documentales", x => x.id);
                table.CheckConstraint("ck_tipos_documentales_dias_alerta", "dias_alerta >= 0");
            });

        migrationBuilder.Sql("""
            INSERT INTO tipos_documentales (
                id, codigo, nombre, aplica_a, obligatorio, critico, bloquea_disponibilidad,
                dias_alerta, requiere_pdf_alerta, activo, created_by_user_id, created_at_utc)
            SELECT gen_random_uuid(), UPPER(TRIM(tipo_documento_codigo)), TRIM(tipo_documento_codigo), 'Activo', false, false, false,
                   30, false, true, 'migration', now()
            FROM documentos
            WHERE tipo_documento_codigo IS NOT NULL AND TRIM(tipo_documento_codigo) <> ''
            GROUP BY UPPER(TRIM(tipo_documento_codigo)), TRIM(tipo_documento_codigo)
            ON CONFLICT DO NOTHING;
            """);

        // Configurable document types are migrated from legacy documents or managed through the application.


        migrationBuilder.AddColumn<string>("ruta_logica", "archivos", type: "character varying(1000)", maxLength: 1000, nullable: true);

        migrationBuilder.AddColumn<string>("descripcion", "documentos", type: "character varying(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<Guid>("tipo_documental_id", "documentos", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("fecha_emision", "documentos", type: "date", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("fecha_vencimiento", "documentos", type: "date", nullable: true);
        migrationBuilder.AddColumn<bool>("vigente", "documentos", type: "boolean", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>("anulado", "documentos", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("anulado_por_usuario_id", "documentos", type: "character varying(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("anulado_at_utc", "documentos", type: "timestamptz", nullable: true);
        migrationBuilder.AddColumn<string>("motivo_anulacion", "documentos", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>("created_by_user_id", "documentos", type: "character varying(120)", maxLength: 120, nullable: false, defaultValue: "migration");
        migrationBuilder.AddColumn<string>("updated_by_user_id", "documentos", type: "character varying(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>("validado_por_usuario_id", "documentos", type: "character varying(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("validado_at_utc", "documentos", type: "timestamptz", nullable: true);
        migrationBuilder.AddColumn<string>("rechazado_por_usuario_id", "documentos", type: "character varying(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("rechazado_at_utc", "documentos", type: "timestamptz", nullable: true);
        migrationBuilder.AddColumn<string>("motivo_rechazo", "documentos", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<bool>("fecha_vencimiento_validada", "documentos", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<Guid>("reemplaza_documento_id", "documentos", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>("reemplazado_por_documento_id", "documentos", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<bool>("historico", "documentos", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("critico", "documentos", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("obligatorio", "documentos", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("bloquea_disponibilidad", "documentos", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("motivo_cambio", "documentos", type: "character varying(500)", maxLength: 500, nullable: true);

        migrationBuilder.Sql("""
            UPDATE documentos d
            SET tipo_documental_id = t.id
            FROM tipos_documentales t
            WHERE t.codigo = UPPER(TRIM(d.tipo_documento_codigo));
            """);

        migrationBuilder.AlterColumn<Guid>("tipo_documental_id", "documentos", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.DropColumn("tipo_documento_codigo", "documentos");

        migrationBuilder.AddColumn<string>("codigo_version", "versiones_documento", type: "character varying(80)", maxLength: 80, nullable: false, defaultValue: "1");
        migrationBuilder.AddColumn<DateTimeOffset>("fecha_carga_utc", "versiones_documento", type: "timestamptz", nullable: false, defaultValueSql: "now()");
        migrationBuilder.AddColumn<string>("cargado_por_usuario_id", "versiones_documento", type: "character varying(120)", maxLength: 120, nullable: false, defaultValue: "migration");
        migrationBuilder.AddColumn<string>("observaciones", "versiones_documento", type: "character varying(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<bool>("vigente", "versiones_documento", type: "boolean", nullable: false, defaultValue: true);

        migrationBuilder.Sql("""
            UPDATE versiones_documento v
            SET codigo_version = v.numero_version::text,
                vigente = v.numero_version = latest.max_version
            FROM (
                SELECT documento_id, MAX(numero_version) AS max_version
                FROM versiones_documento
                GROUP BY documento_id
            ) latest
            WHERE latest.documento_id = v.documento_id;
            """);

        migrationBuilder.CreateTable(
            name: "documento_faenas",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                faena_id = table.Column<Guid>(type: "uuid", nullable: false),
                vigente = table.Column<bool>(type: "boolean", nullable: false),
                asignado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                asignado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                desasignado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                desasignado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                motivo_desasignacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_documento_faenas", x => x.id);
                table.ForeignKey("fk_documento_faenas_documentos_documento_id", x => x.documento_id, "documentos", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_documento_faenas_faenas_faena_id", x => x.faena_id, "faenas", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.DropIndex("ix_documento_activos_documento_id_activo_id_vigente", "documento_activos");

        migrationBuilder.CreateIndex("ix_tipos_documentales_codigo", "tipos_documentales", "codigo", unique: true);
        migrationBuilder.CreateIndex("ix_documentos_tipo_documental_id", "documentos", "tipo_documental_id");
        migrationBuilder.CreateIndex("ix_documentos_estado", "documentos", "estado");
        migrationBuilder.CreateIndex("ix_documentos_reemplaza_documento_id", "documentos", "reemplaza_documento_id");
        migrationBuilder.CreateIndex("ix_documentos_reemplazado_por_documento_id", "documentos", "reemplazado_por_documento_id");
        migrationBuilder.CreateIndex("ix_versiones_documento_documento_id_vigente", "versiones_documento", new[] { "documento_id", "vigente" }, unique: true, filter: "vigente");
        migrationBuilder.CreateIndex("ix_documento_activos_documento_id_activo_id_vigente", "documento_activos", new[] { "documento_id", "activo_id", "vigente" }, unique: true, filter: "vigente");
        migrationBuilder.CreateIndex("ix_documento_faenas_faena_id", "documento_faenas", "faena_id");
        migrationBuilder.CreateIndex("ix_documento_faenas_documento_id_faena_id_vigente", "documento_faenas", new[] { "documento_id", "faena_id", "vigente" }, unique: true, filter: "vigente");

        migrationBuilder.AddForeignKey("fk_documentos_tipos_documentales_tipo_documental_id", "documentos", "tipo_documental_id", "tipos_documentales", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("fk_documentos_documentos_reemplaza_documento_id", "documentos", "reemplaza_documento_id", "documentos", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("fk_documentos_documentos_reemplazado_por_documento_id", "documentos", "reemplazado_por_documento_id", "documentos", principalColumn: "id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("fk_documentos_documentos_reemplaza_documento_id", "documentos");
        migrationBuilder.DropForeignKey("fk_documentos_documentos_reemplazado_por_documento_id", "documentos");
        migrationBuilder.DropForeignKey("fk_documentos_tipos_documentales_tipo_documental_id", "documentos");
        migrationBuilder.DropTable("documento_faenas");
        migrationBuilder.DropIndex("ix_tipos_documentales_codigo", "tipos_documentales");
        migrationBuilder.DropIndex("ix_documentos_tipo_documental_id", "documentos");
        migrationBuilder.DropIndex("ix_documentos_estado", "documentos");
        migrationBuilder.DropIndex("ix_documentos_reemplaza_documento_id", "documentos");
        migrationBuilder.DropIndex("ix_documentos_reemplazado_por_documento_id", "documentos");
        migrationBuilder.DropIndex("ix_versiones_documento_documento_id_vigente", "versiones_documento");
        migrationBuilder.DropIndex("ix_documento_activos_documento_id_activo_id_vigente", "documento_activos");
        migrationBuilder.CreateIndex("ix_documento_activos_documento_id_activo_id_vigente", "documento_activos", new[] { "documento_id", "activo_id", "vigente" }, unique: true);

        migrationBuilder.AddColumn<string>("tipo_documento_codigo", "documentos", type: "character varying(120)", maxLength: 120, nullable: false, defaultValue: "MIGRATED");
        migrationBuilder.Sql("""
            UPDATE documentos d
            SET tipo_documento_codigo = t.codigo
            FROM tipos_documentales t
            WHERE d.tipo_documental_id = t.id;
            """);

        migrationBuilder.DropColumn("ruta_logica", "archivos");
        migrationBuilder.DropColumn("descripcion", "documentos");
        migrationBuilder.DropColumn("tipo_documental_id", "documentos");
        migrationBuilder.DropColumn("fecha_emision", "documentos");
        migrationBuilder.DropColumn("fecha_vencimiento", "documentos");
        migrationBuilder.DropColumn("vigente", "documentos");
        migrationBuilder.DropColumn("anulado", "documentos");
        migrationBuilder.DropColumn("anulado_por_usuario_id", "documentos");
        migrationBuilder.DropColumn("anulado_at_utc", "documentos");
        migrationBuilder.DropColumn("motivo_anulacion", "documentos");
        migrationBuilder.DropColumn("created_by_user_id", "documentos");
        migrationBuilder.DropColumn("updated_by_user_id", "documentos");
        migrationBuilder.DropColumn("validado_por_usuario_id", "documentos");
        migrationBuilder.DropColumn("validado_at_utc", "documentos");
        migrationBuilder.DropColumn("rechazado_por_usuario_id", "documentos");
        migrationBuilder.DropColumn("rechazado_at_utc", "documentos");
        migrationBuilder.DropColumn("motivo_rechazo", "documentos");
        migrationBuilder.DropColumn("fecha_vencimiento_validada", "documentos");
        migrationBuilder.DropColumn("reemplaza_documento_id", "documentos");
        migrationBuilder.DropColumn("reemplazado_por_documento_id", "documentos");
        migrationBuilder.DropColumn("historico", "documentos");
        migrationBuilder.DropColumn("critico", "documentos");
        migrationBuilder.DropColumn("obligatorio", "documentos");
        migrationBuilder.DropColumn("bloquea_disponibilidad", "documentos");
        migrationBuilder.DropColumn("motivo_cambio", "documentos");
        migrationBuilder.DropColumn("codigo_version", "versiones_documento");
        migrationBuilder.DropColumn("fecha_carga_utc", "versiones_documento");
        migrationBuilder.DropColumn("cargado_por_usuario_id", "versiones_documento");
        migrationBuilder.DropColumn("observaciones", "versiones_documento");
        migrationBuilder.DropColumn("vigente", "versiones_documento");
        migrationBuilder.DropTable("tipos_documentales");
    }
}
