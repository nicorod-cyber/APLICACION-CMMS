using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations;

[DbContext(typeof(CmmsDbContext))]
[Migration("202607090001_InitialPostgreSqlSchema")]
public partial class InitialPostgreSqlSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

        migrationBuilder.CreateTable(
            name: "audit_log",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                accion = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                modulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                entidad = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                entidad_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                faena_codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                severidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                valor_anterior = table.Column<string>(type: "text", nullable: true),
                valor_nuevo = table.Column<string>(type: "text", nullable: true),
                ip_address = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                dispositivo = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                exitoso = table.Column<bool>(type: "boolean", nullable: false),
                detalle = table.Column<string>(type: "text", nullable: true),
                correlation_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_audit_log", x => x.id));

        migrationBuilder.CreateTable(
            name: "estados_operacionales_activo",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                activo = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_estados_operacionales_activo", x => x.id);
                table.CheckConstraint("ck_estados_operacionales_activo_codigo", "codigo IN ('OPERATIVO_FAENA','ALERTA_FAENA','FUERA_SERVICIO_FAENA','FUERA_SERVICIO_TALLER')");
            });

        migrationBuilder.CreateTable(
            name: "faenas",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                activo = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_faenas", x => x.id));

        migrationBuilder.CreateTable(
            name: "familias_equipo",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                activo = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_familias_equipo", x => x.id));

        migrationBuilder.CreateTable(
            name: "permisos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                codigo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                activo = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_permisos", x => x.id));

        migrationBuilder.CreateTable(
            name: "roles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                tipo_rol = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                activo = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_roles", x => x.id));

        migrationBuilder.CreateTable(
            name: "usuarios",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                username = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                email = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                activo = table.Column<bool>(type: "boolean", nullable: false),
                bloqueado = table.Column<bool>(type: "boolean", nullable: false),
                password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_usuarios", x => x.id));

        migrationBuilder.CreateTable(
            name: "archivos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                file_key = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                proveedor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                uri_logica = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                tipo_mime = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                tamano_bytes = table.Column<long>(type: "bigint", nullable: true),
                checksum = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                metadata = table.Column<string>(type: "jsonb", nullable: true),
                autor_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_archivos", x => x.id));

        migrationBuilder.CreateTable(
            name: "activos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                faena_id = table.Column<Guid>(type: "uuid", nullable: false),
                familia_equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                estado_operacional_id = table.Column<Guid>(type: "uuid", nullable: false),
                estado_registro = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                tipo_activo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ubicacion_tecnica_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                marca = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                modelo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                patente = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                numero_serie = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                propiedad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                criticidad = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                estado_documental = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                ficha_validada = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_activos", x => x.id);
                table.CheckConstraint("ck_activos_estado_registro", "estado_registro IN ('vigente','inactivo','anulado','obsoleto','reemplazado','no_vigente')");
                table.ForeignKey("fk_activos_estados_operacionales_activo_estado_operacional_id", x => x.estado_operacional_id, "estados_operacionales_activo", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_activos_faenas_faena_id", x => x.faena_id, "faenas", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_activos_familias_equipo_familia_equipo_id", x => x.familia_equipo_id, "familias_equipo", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "documentos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                tipo_documento_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_documentos", x => x.id));

        migrationBuilder.CreateTable(
            name: "rol_permisos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                permiso_id = table.Column<Guid>(type: "uuid", nullable: false),
                vigente = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_rol_permisos", x => x.id);
                table.ForeignKey("fk_rol_permisos_permisos_permiso_id", x => x.permiso_id, "permisos", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_rol_permisos_roles_rol_id", x => x.rol_id, "roles", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "usuario_roles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                vigente = table.Column<bool>(type: "boolean", nullable: false),
                asignado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                asignado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                desasignado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                desasignado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                motivo_desasignacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_usuario_roles", x => x.id);
                table.ForeignKey("fk_usuario_roles_roles_rol_id", x => x.rol_id, "roles", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_usuario_roles_usuarios_usuario_id", x => x.usuario_id, "usuarios", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "usuario_faenas",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                table.PrimaryKey("pk_usuario_faenas", x => x.id);
                table.ForeignKey("fk_usuario_faenas_faenas_faena_id", x => x.faena_id, "faenas", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_usuario_faenas_usuarios_usuario_id", x => x.usuario_id, "usuarios", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "eventos_estado_activo",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                estado_anterior_id = table.Column<Guid>(type: "uuid", nullable: true),
                estado_nuevo_id = table.Column<Guid>(type: "uuid", nullable: false),
                fecha_evento_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_eventos_estado_activo", x => x.id);
                table.ForeignKey("fk_eventos_estado_activo_activos_activo_id", x => x.activo_id, "activos", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_eventos_estado_activo_estados_anterior", x => x.estado_anterior_id, "estados_operacionales_activo", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_eventos_estado_activo_estados_nuevo", x => x.estado_nuevo_id, "estados_operacionales_activo", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "documento_activos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                activo_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                table.PrimaryKey("pk_documento_activos", x => x.id);
                table.ForeignKey("fk_documento_activos_activos_activo_id", x => x.activo_id, "activos", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_documento_activos_documentos_documento_id", x => x.documento_id, "documentos", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "versiones_documento",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                numero_version = table.Column<int>(type: "integer", nullable: false),
                archivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_versiones_documento", x => x.id);
                table.ForeignKey("fk_versiones_documento_archivos_archivo_id", x => x.archivo_id, "archivos", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_versiones_documento_documentos_documento_id", x => x.documento_id, "documentos", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("ix_audit_log_faena_codigo", "audit_log", "faena_codigo");
        migrationBuilder.CreateIndex("ix_audit_log_modulo_entidad", "audit_log", new[] { "modulo", "entidad" });
        migrationBuilder.CreateIndex("ix_audit_log_occurred_at_utc", "audit_log", "occurred_at_utc");
        migrationBuilder.CreateIndex("ix_estados_operacionales_activo_codigo", "estados_operacionales_activo", "codigo", unique: true);
        migrationBuilder.CreateIndex("ix_faenas_codigo", "faenas", "codigo", unique: true);
        migrationBuilder.CreateIndex("ix_familias_equipo_codigo", "familias_equipo", "codigo", unique: true);
        migrationBuilder.CreateIndex("ix_permisos_codigo", "permisos", "codigo", unique: true);
        migrationBuilder.CreateIndex("ix_roles_codigo", "roles", "codigo", unique: true);
        migrationBuilder.CreateIndex("ix_usuarios_email", "usuarios", "email", unique: true);
        migrationBuilder.CreateIndex("ix_usuarios_username", "usuarios", "username", unique: true);
        migrationBuilder.CreateIndex("ix_archivos_file_key", "archivos", "file_key", unique: true);
        migrationBuilder.CreateIndex("ix_activos_codigo", "activos", "codigo", unique: true);
        migrationBuilder.CreateIndex("ix_activos_estado_operacional_id", "activos", "estado_operacional_id");
        migrationBuilder.CreateIndex("ix_activos_faena_id", "activos", "faena_id");
        migrationBuilder.CreateIndex("ix_activos_familia_equipo_id", "activos", "familia_equipo_id");
        migrationBuilder.CreateIndex("ix_documentos_codigo", "documentos", "codigo", unique: true);
        migrationBuilder.CreateIndex("ix_rol_permisos_permiso_id", "rol_permisos", "permiso_id");
        migrationBuilder.CreateIndex("ix_rol_permisos_rol_id_permiso_id_vigente", "rol_permisos", new[] { "rol_id", "permiso_id", "vigente" }, unique: true);
        migrationBuilder.CreateIndex("ix_usuario_roles_rol_id", "usuario_roles", "rol_id");
        migrationBuilder.CreateIndex("ix_usuario_roles_usuario_id_rol_id_vigente", "usuario_roles", new[] { "usuario_id", "rol_id", "vigente" }, unique: true);
        migrationBuilder.CreateIndex("ix_usuario_faenas_faena_id", "usuario_faenas", "faena_id");
        migrationBuilder.CreateIndex("ix_usuario_faenas_usuario_id_faena_id_vigente", "usuario_faenas", new[] { "usuario_id", "faena_id", "vigente" }, unique: true);
        migrationBuilder.CreateIndex("ix_eventos_estado_activo_activo_id", "eventos_estado_activo", "activo_id");
        migrationBuilder.CreateIndex("ix_eventos_estado_activo_estado_anterior_id", "eventos_estado_activo", "estado_anterior_id");
        migrationBuilder.CreateIndex("ix_eventos_estado_activo_estado_nuevo_id", "eventos_estado_activo", "estado_nuevo_id");
        migrationBuilder.CreateIndex("ix_documento_activos_activo_id", "documento_activos", "activo_id");
        migrationBuilder.CreateIndex("ix_documento_activos_documento_id_activo_id_vigente", "documento_activos", new[] { "documento_id", "activo_id", "vigente" }, unique: true);
        migrationBuilder.CreateIndex("ix_versiones_documento_archivo_id", "versiones_documento", "archivo_id");
        migrationBuilder.CreateIndex("ix_versiones_documento_documento_id_numero_version", "versiones_documento", new[] { "documento_id", "numero_version" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("documento_activos");
        migrationBuilder.DropTable("eventos_estado_activo");
        migrationBuilder.DropTable("rol_permisos");
        migrationBuilder.DropTable("usuario_faenas");
        migrationBuilder.DropTable("usuario_roles");
        migrationBuilder.DropTable("versiones_documento");
        migrationBuilder.DropTable("audit_log");
        migrationBuilder.DropTable("activos");
        migrationBuilder.DropTable("permisos");
        migrationBuilder.DropTable("roles");
        migrationBuilder.DropTable("usuarios");
        migrationBuilder.DropTable("archivos");
        migrationBuilder.DropTable("documentos");
        migrationBuilder.DropTable("estados_operacionales_activo");
        migrationBuilder.DropTable("faenas");
        migrationBuilder.DropTable("familias_equipo");
    }
}
