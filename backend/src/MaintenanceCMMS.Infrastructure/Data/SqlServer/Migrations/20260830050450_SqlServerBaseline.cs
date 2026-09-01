using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SqlServerBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archivos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    file_key = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    nombre_almacenado = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    extension = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    proveedor = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    modo_almacenamiento = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    proposito = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    modulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    tipo_entidad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    entidad_id = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    faena_codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    activo_codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    numero_ot = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    uri_logica = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ruta_logica = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ubicacion_fisica = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    tipo_mime = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: true),
                    checksum = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    version_archivo = table.Column<int>(type: "int", nullable: false),
                    eliminado = table.Column<bool>(type: "bit", nullable: false),
                    eliminado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    eliminado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    autor_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archivos", x => x.id);
                    table.CheckConstraint("ck_archivos_estado", "estado IN ('Stored','ManualLink','PendingManualLink','GraphApiReady','InvalidPath','Deleted')");
                    table.CheckConstraint("ck_archivos_proveedor", "proveedor IN ('ManualLink','LocalSimulation','GraphApiReady')");
                    table.CheckConstraint("ck_archivos_tamano_no_negativo", "tamano_bytes IS NULL OR tamano_bytes >= 0");
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    accion = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    modulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    entidad = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    entidad_id = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    faena_codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    severidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    valor_anterior = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    valor_nuevo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    dispositivo = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    exitoso = table.Column<bool>(type: "bit", nullable: false),
                    detalle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    correlation_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "catalogos_inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    categoria = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    orden = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogos_inventario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "catalogos_trabajo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    categoria = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    orden = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogos_trabajo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "estados_operacionales_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    severidad = table.Column<int>(type: "int", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estados_operacionales_activo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permisos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plantillas_checklist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    tipo_ot_codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    familia_codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    plan_preventivo_codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    tarea_codigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    activo_codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plantillas_checklist", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "proveedores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rut = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    contacto = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    email = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    telefono = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    direccion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    lead_time_esperado_dias = table.Column<int>(type: "int", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proveedores", x => x.id);
                    table.CheckConstraint("ck_proveedores_lead_time", "lead_time_esperado_dias >= 0");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Latin1_General_100_CS_AS"),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    tipo_rol = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles_componente_unidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    critico = table.Column<bool>(type: "bit", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles_componente_unidad", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tarifas_hh",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    especialidad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    tarifa_hora = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarifas_hh", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipos_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    categoria = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    es_movil = table.Column<bool>(type: "bit", nullable: false),
                    es_montable = table.Column<bool>(type: "bit", nullable: false),
                    puede_ser_portador = table.Column<bool>(type: "bit", nullable: false),
                    controla_mantenimiento = table.Column<bool>(type: "bit", nullable: false),
                    participa_en_disponibilidad = table.Column<bool>(type: "bit", nullable: false),
                    orden_visualizacion = table.Column<int>(type: "int", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_activo", x => x.id);
                    table.CheckConstraint("ck_tipos_activo_orden", "orden_visualizacion >= 0");
                });

            migrationBuilder.CreateTable(
                name: "tipos_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    aplica_a = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    obligatorio = table.Column<bool>(type: "bit", nullable: false),
                    critico = table.Column<bool>(type: "bit", nullable: false),
                    bloquea_disponibilidad = table.Column<bool>(type: "bit", nullable: false),
                    dias_alerta = table.Column<int>(type: "int", nullable: false),
                    roles_responsables = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    requiere_pdf_alerta = table.Column<bool>(type: "bit", nullable: false),
                    plantilla_html_codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    extensiones_permitidas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    tamano_maximo_bytes = table.Column<long>(type: "bigint", nullable: true),
                    storage_destination_key = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    plantilla_carpeta = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    updated_by_user_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_documentales", x => x.id);
                    table.CheckConstraint("ck_tipos_documentales_dias_alerta", "dias_alerta >= 0");
                    table.CheckConstraint("ck_tipos_documentales_tamano_maximo", "tamano_maximo_bytes IS NULL OR tamano_maximo_bytes > 0");
                });

            migrationBuilder.CreateTable(
                name: "tipos_unidad_operativa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    participa_en_disponibilidad = table.Column<bool>(type: "bit", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_unidad_operativa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    username = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    bloqueado = table.Column<bool>(type: "bit", nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "importaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    entidad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    esquema = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    archivo_original = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    archivo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    solo_simulacion = table.Column<bool>(type: "bit", nullable: false),
                    estado = table.Column<int>(type: "int", nullable: false),
                    cargado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    cargado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    aplicado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    aplicado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    rechazado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    rechazado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    motivo_rechazo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_importaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_importaciones_archivos_archivo_id",
                        column: x => x.archivo_id,
                        principalTable: "archivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plantillas_pdf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    tipo_evento = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    asunto_plantilla = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    html_plantilla = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    version_plantilla = table.Column<int>(type: "int", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    archivo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plantillas_pdf", x => x.id);
                    table.CheckConstraint("ck_plantillas_pdf_version", "version_plantilla >= 1");
                    table.ForeignKey(
                        name: "FK_plantillas_pdf_archivos_archivo_id",
                        column: x => x.archivo_id,
                        principalTable: "archivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "repuestos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    codigo_sap = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    codigo_proveedor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    descripcion_tecnica = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    unidad_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    fabricante = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    modelo_referencia = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    critico = table.Column<bool>(type: "bit", nullable: false),
                    stock_minimo = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    stock_maximo = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    punto_reposicion = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    lead_time_dias = table.Column<int>(type: "int", nullable: false),
                    costo_unitario_promedio = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    proveedor_preferente = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    reemplazo_codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "items_plantilla_checklist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden = table.Column<int>(type: "int", nullable: false),
                    texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    obligatorio = table.Column<bool>(type: "bit", nullable: false),
                    tipo_respuesta_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    requiere_foto = table.Column<bool>(type: "bit", nullable: false),
                    requiere_archivo = table.Column<bool>(type: "bit", nullable: false),
                    requiere_firma = table.Column<bool>(type: "bit", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items_plantilla_checklist", x => x.id);
                    table.ForeignKey(
                        name: "FK_items_plantilla_checklist_catalogos_trabajo_tipo_respuesta_id",
                        column: x => x.tipo_respuesta_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_items_plantilla_checklist_plantillas_checklist_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "plantillas_checklist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "planes_preventivos_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    tipo_frecuencia = table.Column<int>(type: "int", nullable: false),
                    frecuencia_horas = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    frecuencia_km = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    frecuencia_dias = table.Column<int>(type: "int", nullable: true),
                    tolerancia_horas = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    tolerancia_km = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    tolerancia_dias = table.Column<int>(type: "int", nullable: false),
                    plantilla_checklist_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    repuestos_sugeridos = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    hh_estimadas = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    fecha_inicio_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ultima_ejecucion_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ultima_ejecucion_horas = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ultima_ejecucion_km = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    proxima_fecha_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    proxima_hora = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    proximo_km = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planes_preventivos_sql", x => x.id);
                    table.CheckConstraint("ck_planes_preventivos_frecuencias", "frecuencia_horas IS NOT NULL OR frecuencia_km IS NOT NULL OR frecuencia_dias IS NOT NULL");
                    table.CheckConstraint("ck_planes_preventivos_hh", "hh_estimadas > 0");
                    table.ForeignKey(
                        name: "FK_planes_preventivos_sql_plantillas_checklist_plantilla_checklist_id",
                        column: x => x.plantilla_checklist_id,
                        principalTable: "plantillas_checklist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rol_permisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rol_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    permiso_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rol_permisos", x => x.id);
                    table.ForeignKey(
                        name: "FK_rol_permisos_permisos_permiso_id",
                        column: x => x.permiso_id,
                        principalTable: "permisos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rol_permisos_roles_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "familias_equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    marca_referencia = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    modelo_referencia = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_familias_equipo", x => x.id);
                    table.ForeignKey(
                        name: "FK_familias_equipo_tipos_activo_tipo_activo_id",
                        column: x => x.tipo_activo_id,
                        principalTable: "tipos_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reglas_composicion_unidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rol_componente_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cantidad_minima = table.Column<int>(type: "int", nullable: false),
                    cantidad_maxima = table.Column<int>(type: "int", nullable: false),
                    obligatorio = table.Column<bool>(type: "bit", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reglas_composicion_unidad", x => x.id);
                    table.CheckConstraint("ck_reglas_composicion_cantidades", "cantidad_minima >= 0 AND cantidad_maxima >= cantidad_minima");
                    table.CheckConstraint("ck_reglas_composicion_obligatorio", "(obligatorio = 1 AND cantidad_minima > 0) OR (obligatorio = 0 AND cantidad_minima = 0)");
                    table.ForeignKey(
                        name: "FK_reglas_composicion_unidad_roles_componente_unidad_rol_componente_id",
                        column: x => x.rol_componente_id,
                        principalTable: "roles_componente_unidad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reglas_composicion_unidad_tipos_unidad_operativa_tipo_unidad_operativa_id",
                        column: x => x.tipo_unidad_operativa_id,
                        principalTable: "tipos_unidad_operativa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "faenas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    zona = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    cliente = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    centro_costes = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    administrador_contrato = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    tipo_faena = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    region = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    comuna = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    latitud = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    longitud = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    responsable_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faenas", x => x.id);
                    table.CheckConstraint("ck_faenas_latitud", "latitud IS NULL OR latitud BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_faenas_longitud", "longitud IS NULL OR longitud BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_faenas_zona_valida", "zona IS NULL OR zona IN ('Zona 0', 'Zona 1', 'Zona 2', 'Zona 3', 'Zona 4')");
                    table.ForeignKey(
                        name: "FK_faenas_usuarios_responsable_usuario_id",
                        column: x => x.responsable_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "talleres",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    capacidad_equipos = table.Column<int>(type: "int", nullable: false),
                    comuna = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    supervisor_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_talleres", x => x.id);
                    table.CheckConstraint("ck_talleres_capacidad_equipos", "capacidad_equipos >= 0");
                    table.ForeignKey(
                        name: "FK_talleres_usuarios_supervisor_usuario_id",
                        column: x => x.supervisor_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuario_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rol_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    asignado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    asignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    desasignado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    desasignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_desasignacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_roles_roles_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_usuario_roles_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "errores_importacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    importacion_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero_fila = table.Column<int>(type: "int", nullable: false),
                    columna = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    mensaje = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_errores_importacion", x => x.id);
                    table.ForeignKey(
                        name: "FK_errores_importacion_importaciones_importacion_id",
                        column: x => x.importacion_id,
                        principalTable: "importaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eventos_importacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    importacion_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado = table.Column<int>(type: "int", nullable: false),
                    usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    detalle = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_importacion", x => x.id);
                    table.ForeignKey(
                        name: "FK_eventos_importacion_importaciones_importacion_id",
                        column: x => x.importacion_id,
                        principalTable: "importaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "filas_importacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    importacion_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero_fila = table.Column<int>(type: "int", nullable: false),
                    operacion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    snapshot_entrada = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_filas_importacion", x => x.id);
                    table.ForeignKey(
                        name: "FK_filas_importacion_importaciones_importacion_id",
                        column: x => x.importacion_id,
                        principalTable: "importaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "definiciones_atributo_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    familia_equipo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    tipo_dato = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    unidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    obligatorio = table.Column<bool>(type: "bit", nullable: false),
                    es_identificador = table.Column<bool>(type: "bit", nullable: false),
                    es_unico = table.Column<bool>(type: "bit", nullable: false),
                    permite_busqueda = table.Column<bool>(type: "bit", nullable: false),
                    permite_filtro = table.Column<bool>(type: "bit", nullable: false),
                    mostrar_en_listado = table.Column<bool>(type: "bit", nullable: false),
                    valor_minimo = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    valor_maximo = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    patron_validacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    opciones_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    grupo_visualizacion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    orden_visualizacion = table.Column<int>(type: "int", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_definiciones_atributo_activo", x => x.id);
                    table.CheckConstraint("ck_definiciones_atributo_rango", "valor_minimo IS NULL OR valor_maximo IS NULL OR valor_minimo <= valor_maximo");
                    table.CheckConstraint("ck_definiciones_atributo_tipo", "tipo_dato IN ('TEXTO','NUMERO','ENTERO','BOOLEANO','FECHA','OPCION')");
                    table.ForeignKey(
                        name: "FK_definiciones_atributo_activo_familias_equipo_familia_equipo_id",
                        column: x => x.familia_equipo_id,
                        principalTable: "familias_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_definiciones_atributo_activo_tipos_activo_tipo_activo_id",
                        column: x => x.tipo_activo_id,
                        principalTable: "tipos_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "requisitos_documentales_tipo_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    familia_equipo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    tipo_documental_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    obligatorio = table.Column<bool>(type: "bit", nullable: false),
                    critico = table.Column<bool>(type: "bit", nullable: false),
                    bloquea_disponibilidad = table.Column<bool>(type: "bit", nullable: false),
                    requiere_fecha_vencimiento = table.Column<bool>(type: "bit", nullable: false),
                    dias_alerta = table.Column<int>(type: "int", nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requisitos_documentales_tipo_activo", x => x.id);
                    table.CheckConstraint("ck_requisitos_documentales_dias_alerta", "dias_alerta IS NULL OR dias_alerta >= 0");
                    table.ForeignKey(
                        name: "FK_requisitos_documentales_tipo_activo_familias_equipo_familia_equipo_id",
                        column: x => x.familia_equipo_id,
                        principalTable: "familias_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_requisitos_documentales_tipo_activo_tipos_activo_tipo_activo_id",
                        column: x => x.tipo_activo_id,
                        principalTable: "tipos_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_requisitos_documentales_tipo_activo_tipos_documentales_tipo_documental_id",
                        column: x => x.tipo_documental_id,
                        principalTable: "tipos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reglas_composicion_unidad_activos_permitidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    regla_composicion_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    familia_equipo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reglas_composicion_unidad_activos_permitidos", x => x.id);
                    table.CheckConstraint("ck_reglas_composicion_permitidos_objetivo", "tipo_activo_id IS NOT NULL OR familia_equipo_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_reglas_composicion_unidad_activos_permitidos_familias_equipo_familia_equipo_id",
                        column: x => x.familia_equipo_id,
                        principalTable: "familias_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reglas_composicion_unidad_activos_permitidos_reglas_composicion_unidad_regla_composicion_id",
                        column: x => x.regla_composicion_id,
                        principalTable: "reglas_composicion_unidad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reglas_composicion_unidad_activos_permitidos_tipos_activo_tipo_activo_id",
                        column: x => x.tipo_activo_id,
                        principalTable: "tipos_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "activos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    tipo_activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    familia_equipo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    estado_operacional_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    marca = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    modelo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    numero_serie = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    propiedad = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    criticidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    anio_fabricacion = table.Column<short>(type: "smallint", nullable: true),
                    fecha_adquisicion = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_puesta_servicio = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_baja = table.Column<DateOnly>(type: "date", nullable: true),
                    tipo_medicion_uso = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activos", x => x.id);
                    table.CheckConstraint("ck_activos_anio_fabricacion", "anio_fabricacion IS NULL OR anio_fabricacion BETWEEN 1900 AND 2200");
                    table.CheckConstraint("ck_activos_fecha_baja", "fecha_baja IS NULL OR fecha_puesta_servicio IS NULL OR fecha_baja >= fecha_puesta_servicio");
                    table.CheckConstraint("ck_activos_tipo_medicion_uso", "tipo_medicion_uso IS NULL OR tipo_medicion_uso IN ('HOROMETRO','KILOMETRAJE')");
                    table.ForeignKey(
                        name: "FK_activos_estados_operacionales_activo_estado_operacional_id",
                        column: x => x.estado_operacional_id,
                        principalTable: "estados_operacionales_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activos_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activos_familias_equipo_familia_equipo_id",
                        column: x => x.familia_equipo_id,
                        principalTable: "familias_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activos_tipos_activo_tipo_activo_id",
                        column: x => x.tipo_activo_id,
                        principalTable: "tipos_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bodegas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ubicacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    responsable_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    permite_stock_negativo = table.Column<bool>(type: "bit", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "contratos_disponibilidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    cliente = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    horas_comprometidas_dia = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    disponibilidad_objetivo = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    fecha_inicio_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    fecha_fin_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    reglas_cliente = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contratos_disponibilidad", x => x.id);
                    table.CheckConstraint("ck_contratos_disponibilidad_fechas", "fecha_fin_utc IS NULL OR fecha_inicio_utc IS NULL OR fecha_fin_utc >= fecha_inicio_utc");
                    table.CheckConstraint("ck_contratos_disponibilidad_horas", "horas_comprometidas_dia > 0");
                    table.CheckConstraint("ck_contratos_disponibilidad_objetivo", "disponibilidad_objetivo >= 0 AND disponibilidad_objetivo <= 1");
                    table.ForeignKey(
                        name: "FK_contratos_disponibilidad_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estados_pago",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    proveedor_rut = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ContractCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FaenaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "matrices_requisitos_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    numero_version = table.Column<int>(type: "int", nullable: false),
                    vigencia_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    tipo_activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    familia_equipo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    motivo_cambio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matrices_requisitos_documentales", x => x.id);
                    table.CheckConstraint("ck_matrices_requisitos_estado", "estado IN ('BORRADOR','VIGENTE','REEMPLAZADA','ANULADA')");
                    table.CheckConstraint("ck_matrices_requisitos_version", "numero_version > 0");
                    table.CheckConstraint("ck_matrices_requisitos_vigencias", "vigencia_hasta IS NULL OR vigencia_hasta >= vigencia_desde");
                    table.ForeignKey(
                        name: "FK_matrices_requisitos_documentales_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_matrices_requisitos_documentales_familias_equipo_familia_equipo_id",
                        column: x => x.familia_equipo_id,
                        principalTable: "familias_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_matrices_requisitos_documentales_tipos_activo_tipo_activo_id",
                        column: x => x.tipo_activo_id,
                        principalTable: "tipos_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nodos_tecnicos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    nombre_normalizado = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    nivel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    nodo_padre_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    obsoleto = table.Column<bool>(type: "bit", nullable: false),
                    fusionado_en_nodo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodos_tecnicos", x => x.id);
                    table.CheckConstraint("ck_nodos_tecnicos_nivel", "nivel IN ('Sistema','Subsistema','Componente','Subcomponente')");
                    table.CheckConstraint("ck_nodos_tecnicos_no_self_merge", "fusionado_en_nodo_id IS NULL OR fusionado_en_nodo_id <> id");
                    table.CheckConstraint("ck_nodos_tecnicos_no_self_parent", "nodo_padre_id IS NULL OR nodo_padre_id <> id");
                    table.ForeignKey(
                        name: "FK_nodos_tecnicos_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nodos_tecnicos_nodos_tecnicos_fusionado_en_nodo_id",
                        column: x => x.fusionado_en_nodo_id,
                        principalTable: "nodos_tecnicos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nodos_tecnicos_nodos_tecnicos_nodo_padre_id",
                        column: x => x.nodo_padre_id,
                        principalTable: "nodos_tecnicos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reglas_alerta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    tipo_evento = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    activa = table.Column<bool>(type: "bit", nullable: false),
                    severidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    repetir_hasta_resolver = table.Column<bool>(type: "bit", nullable: false),
                    genera_email = table.Column<bool>(type: "bit", nullable: false),
                    genera_pdf = table.Column<bool>(type: "bit", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reglas_alerta", x => x.id);
                    table.CheckConstraint("ck_reglas_alerta_severidad", "severidad IN ('Info','Warning','Critical')");
                    table.ForeignKey(
                        name: "FK_reglas_alerta_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reglas_alerta_plantillas_pdf_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "plantillas_pdf",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ubicaciones_tecnicas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    obsoleto = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ubicaciones_tecnicas", x => x.id);
                    table.ForeignKey(
                        name: "FK_ubicaciones_tecnicas_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuario_faenas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    asignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    asignado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    desasignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    desasignado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    motivo_desasignacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_faenas", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_faenas_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_usuario_faenas_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alias_identificador_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_identificador = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ambito = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    valor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    valor_normalizado = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    vigencia_desde_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    vigencia_hasta_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    reemplazado_por_alias_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alias_identificador_activo", x => x.id);
                    table.CheckConstraint("ck_alias_identificador_vigencias", "vigencia_hasta_utc IS NULL OR vigencia_hasta_utc > vigencia_desde_utc");
                    table.ForeignKey(
                        name: "FK_alias_identificador_activo_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alias_identificador_activo_alias_identificador_activo_reemplazado_por_alias_id",
                        column: x => x.reemplazado_por_alias_id,
                        principalTable: "alias_identificador_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eventos_estado_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado_anterior_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    estado_nuevo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fecha_evento_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    tipo_antecedente = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    antecedente_id = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    referencia_antecedente = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_estado_activo", x => x.id);
                    table.ForeignKey(
                        name: "FK_eventos_estado_activo_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eventos_estado_activo_estados_operacionales_activo_estado_anterior_id",
                        column: x => x.estado_anterior_id,
                        principalTable: "estados_operacionales_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eventos_estado_activo_estados_operacionales_activo_estado_nuevo_id",
                        column: x => x.estado_nuevo_id,
                        principalTable: "estados_operacionales_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unidades_operativas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    tipo_unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    estado_operacional_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado_operacional_base_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    estado_derivado_por_activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    motivo_estado_derivado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    estado_derivado_calculado_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    criticidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    fecha_puesta_servicio = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_baja = table.Column<DateOnly>(type: "date", nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidades_operativas", x => x.id);
                    table.CheckConstraint("ck_unidades_operativas_fecha_baja", "fecha_baja IS NULL OR fecha_puesta_servicio IS NULL OR fecha_baja >= fecha_puesta_servicio");
                    table.ForeignKey(
                        name: "FK_unidades_operativas_activos_estado_derivado_por_activo_id",
                        column: x => x.estado_derivado_por_activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_unidades_operativas_estados_operacionales_activo_estado_operacional_base_id",
                        column: x => x.estado_operacional_base_id,
                        principalTable: "estados_operacionales_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_unidades_operativas_estados_operacionales_activo_estado_operacional_id",
                        column: x => x.estado_operacional_id,
                        principalTable: "estados_operacionales_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_unidades_operativas_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_unidades_operativas_tipos_unidad_operativa_tipo_unidad_operativa_id",
                        column: x => x.tipo_unidad_operativa_id,
                        principalTable: "tipos_unidad_operativa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "valores_atributo_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    definicion_atributo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    valor_texto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    valor_numerico = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    valor_booleano = table.Column<bool>(type: "bit", nullable: true),
                    valor_fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_valores_atributo_activo", x => x.id);
                    table.CheckConstraint("ck_valores_atributo_un_valor", "(CASE WHEN valor_texto IS NULL THEN 0 ELSE 1 END + CASE WHEN valor_numerico IS NULL THEN 0 ELSE 1 END + CASE WHEN valor_booleano IS NULL THEN 0 ELSE 1 END + CASE WHEN valor_fecha IS NULL THEN 0 ELSE 1 END) <= 1");
                    table.ForeignKey(
                        name: "FK_valores_atributo_activo_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valores_atributo_activo_definiciones_atributo_activo_definicion_atributo_id",
                        column: x => x.definicion_atributo_id,
                        principalTable: "definiciones_atributo_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transferencias_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    bodega_origen_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    bodega_transito_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    bodega_destino_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    solicitado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    solicitado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    recibido_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    recibido_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    motivo_recepcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    motivo_anulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    pasillo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    estante = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    nivel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    posicion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "detalles_matriz_requisitos_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    matriz_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_documental_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    obligatorio = table.Column<bool>(type: "bit", nullable: false),
                    critico = table.Column<bool>(type: "bit", nullable: false),
                    bloquea_disponibilidad = table.Column<bool>(type: "bit", nullable: false),
                    requiere_fecha_vencimiento = table.Column<bool>(type: "bit", nullable: false),
                    dias_anticipacion = table.Column<int>(type: "int", nullable: false),
                    reutilizable_entre_faenas = table.Column<bool>(type: "bit", nullable: false),
                    orden_presentacion = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalles_matriz_requisitos_documentales", x => x.id);
                    table.CheckConstraint("ck_detalles_matriz_dias_anticipacion", "dias_anticipacion >= 0");
                    table.ForeignKey(
                        name: "FK_detalles_matriz_requisitos_documentales_matrices_requisitos_documentales_matriz_id",
                        column: x => x.matriz_id,
                        principalTable: "matrices_requisitos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalles_matriz_requisitos_documentales_tipos_documentales_tipo_documental_id",
                        column: x => x.tipo_documental_id,
                        principalTable: "tipos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nodo_tecnico_activos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nodo_tecnico_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodo_tecnico_activos", x => x.id);
                    table.ForeignKey(
                        name: "FK_nodo_tecnico_activos_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nodo_tecnico_activos_nodos_tecnicos_nodo_tecnico_id",
                        column: x => x.nodo_tecnico_id,
                        principalTable: "nodos_tecnicos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nodo_tecnico_aliases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nodo_tecnico_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    alias = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    alias_normalizado = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    origen = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodo_tecnico_aliases", x => x.id);
                    table.ForeignKey(
                        name: "FK_nodo_tecnico_aliases_nodos_tecnicos_nodo_tecnico_id",
                        column: x => x.nodo_tecnico_id,
                        principalTable: "nodos_tecnicos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nodo_tecnico_familias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nodo_tecnico_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    familia_equipo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodo_tecnico_familias", x => x.id);
                    table.ForeignKey(
                        name: "FK_nodo_tecnico_familias_familias_equipo_familia_equipo_id",
                        column: x => x.familia_equipo_id,
                        principalTable: "familias_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nodo_tecnico_familias_nodos_tecnicos_nodo_tecnico_id",
                        column: x => x.nodo_tecnico_id,
                        principalTable: "nodos_tecnicos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alertas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    regla_alerta_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    titulo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    mensaje = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    severidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    origen = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    clave_causa = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    clave_deduplicacion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    tipo_entidad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    entidad_id = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    repeticion_critica = table.Column<bool>(type: "bit", nullable: false),
                    cantidad_repeticiones = table.Column<int>(type: "int", nullable: false),
                    reconocido_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    reconocido_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    resuelto_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    resuelto_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    motivo_resolucion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    activa = table.Column<bool>(type: "bit", nullable: false),
                    archivo_pdf_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas", x => x.id);
                    table.CheckConstraint("ck_alertas_estado", "estado IN ('Open','Acknowledged','Resolved')");
                    table.CheckConstraint("ck_alertas_repeticiones", "cantidad_repeticiones >= 1");
                    table.CheckConstraint("ck_alertas_severidad", "severidad IN ('Info','Warning','Critical')");
                    table.ForeignKey(
                        name: "FK_alertas_archivos_archivo_pdf_id",
                        column: x => x.archivo_pdf_id,
                        principalTable: "archivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alertas_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alertas_reglas_alerta_regla_alerta_id",
                        column: x => x.regla_alerta_id,
                        principalTable: "reglas_alerta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "regla_alerta_destinatarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    regla_alerta_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    rol_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    destino = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    canal = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regla_alerta_destinatarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_regla_alerta_destinatarios_reglas_alerta_regla_alerta_id",
                        column: x => x.regla_alerta_id,
                        principalTable: "reglas_alerta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_regla_alerta_destinatarios_roles_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_regla_alerta_destinatarios_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alcances_plan_preventivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    plan_preventivo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    familia_equipo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    tipo_activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    marca = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    modelo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alcances_plan_preventivo", x => x.id);
                    table.CheckConstraint("ck_alcances_plan_preventivo_objetivo", "activo_id IS NOT NULL OR familia_equipo_id IS NOT NULL OR tipo_activo_id IS NOT NULL OR unidad_operativa_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_alcances_plan_preventivo_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alcances_plan_preventivo_familias_equipo_familia_equipo_id",
                        column: x => x.familia_equipo_id,
                        principalTable: "familias_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alcances_plan_preventivo_planes_preventivos_sql_plan_preventivo_id",
                        column: x => x.plan_preventivo_id,
                        principalTable: "planes_preventivos_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alcances_plan_preventivo_tipos_activo_tipo_activo_id",
                        column: x => x.tipo_activo_id,
                        principalTable: "tipos_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alcances_plan_preventivo_unidades_operativas_unidad_operativa_id",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contrato_disponibilidad_objetivos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    rol = table.Column<int>(type: "int", nullable: false),
                    fecha_inicio_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    fecha_fin_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contrato_disponibilidad_objetivos", x => x.id);
                    table.CheckConstraint("ck_contrato_disponibilidad_objetivos_fechas", "fecha_fin_utc IS NULL OR fecha_inicio_utc IS NULL OR fecha_fin_utc >= fecha_inicio_utc");
                    table.CheckConstraint("ck_contrato_disponibilidad_objetivos_un_objetivo", "(CASE WHEN activo_id IS NULL THEN 0 ELSE 1 END + CASE WHEN unidad_operativa_id IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_contrato_disponibilidad_objetivos_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contrato_disponibilidad_objetivos_contratos_disponibilidad_contrato_id",
                        column: x => x.contrato_id,
                        principalTable: "contratos_disponibilidad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contrato_disponibilidad_objetivos_unidades_operativas_unidad_operativa_id",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_trabajo_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero_ot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_mantenimiento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    aviso_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    sistema = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    subsistema = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    componente = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    prioridad_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    criticidad_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    clasificacion_falla_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    plan_preventivo_codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    plantilla_preventiva_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    plantilla_preventiva_version_snapshot = table.Column<int>(type: "int", nullable: true),
                    preventiva_automatica = table.Column<bool>(type: "bit", nullable: false),
                    matriz_documental_version_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    requiere_firma = table.Column<bool>(type: "bit", nullable: false),
                    fecha_programada_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    inicio_programado_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    fin_programado_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    creado_por_usuario_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    supervisor_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    supervisor_nombre_snapshot = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    supervisor_asignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    supervisor_asignado_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    motivo_reasignacion_supervisor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    inicio_real_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    finalizacion_tecnico_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    finalizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    cierre_supervisor_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cerrado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    validacion_planificacion_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    validado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    anulado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    anulado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_anulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    actualizado_por_usuario_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordenes_trabajo_sql", x => x.id);
                    table.CheckConstraint("ck_ordenes_trabajo_sql_objetivo", "activo_id IS NOT NULL OR unidad_operativa_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_ordenes_trabajo_sql_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordenes_trabajo_sql_catalogos_trabajo_clasificacion_falla_id",
                        column: x => x.clasificacion_falla_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordenes_trabajo_sql_catalogos_trabajo_criticidad_id",
                        column: x => x.criticidad_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordenes_trabajo_sql_catalogos_trabajo_estado_id",
                        column: x => x.estado_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordenes_trabajo_sql_catalogos_trabajo_prioridad_id",
                        column: x => x.prioridad_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordenes_trabajo_sql_catalogos_trabajo_tipo_mantenimiento_id",
                        column: x => x.tipo_mantenimiento_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordenes_trabajo_sql_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordenes_trabajo_sql_unidades_operativas_unidad_operativa_id",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ot_matriz_documental",
                        column: x => x.matriz_documental_version_id,
                        principalTable: "matrices_requisitos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ot_plantilla_preventiva",
                        column: x => x.plantilla_preventiva_id,
                        principalTable: "plantillas_checklist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ot_supervisor",
                        column: x => x.supervisor_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ot_supervisor_asignador",
                        column: x => x.supervisor_asignado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "traslados_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    faena_origen_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    faena_destino_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    fecha_efectiva_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    fecha_registro_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_traslados_activo", x => x.id);
                    table.CheckConstraint("ck_traslados_activo_origen_destino", "faena_destino_id IS NOT NULL AND (faena_origen_id IS NULL OR faena_origen_id <> faena_destino_id)");
                    table.ForeignKey(
                        name: "FK_traslados_activo_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_traslados_activo_faenas_faena_destino_id",
                        column: x => x.faena_destino_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_traslados_activo_faenas_faena_origen_id",
                        column: x => x.faena_origen_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_traslados_activo_unidades_operativas_unidad_operativa_id",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_bodega",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ubicacion_bodega_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    cantidad_fisica = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_reservada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    stock_minimo_especifico = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    titulo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    tipo_documental_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    fecha_emision = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    anulado = table.Column<bool>(type: "bit", nullable: false),
                    anulado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    anulado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_anulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    updated_by_user_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    validado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    validado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    rechazado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    rechazado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    fecha_vencimiento_validada = table.Column<bool>(type: "bit", nullable: false),
                    reemplaza_documento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reemplazado_por_documento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    historico = table.Column<bool>(type: "bit", nullable: false),
                    critico = table.Column<bool>(type: "bit", nullable: false),
                    obligatorio = table.Column<bool>(type: "bit", nullable: false),
                    bloquea_disponibilidad = table.Column<bool>(type: "bit", nullable: false),
                    motivo_cambio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    matriz_requisito_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    requisito_matriz_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    activo_requisito_activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_documentos_activos_activo_requisito_activo_id",
                        column: x => x.activo_requisito_activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documentos_detalles_matriz_requisitos_documentales_requisito_matriz_item_id",
                        column: x => x.requisito_matriz_item_id,
                        principalTable: "detalles_matriz_requisitos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documentos_documentos_reemplaza_documento_id",
                        column: x => x.reemplaza_documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documentos_documentos_reemplazado_por_documento_id",
                        column: x => x.reemplazado_por_documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documentos_matrices_requisitos_documentales_matriz_requisito_id",
                        column: x => x.matriz_requisito_id,
                        principalTable: "matrices_requisitos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documentos_tipos_documentales_tipo_documental_id",
                        column: x => x.tipo_documental_id,
                        principalTable: "tipos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notificaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    alerta_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    canal = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    asunto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    cuerpo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    programado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    enviado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cantidad_intentos = table.Column<int>(type: "int", nullable: false),
                    proveedor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ultimo_error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    archivo_pdf_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificaciones", x => x.id);
                    table.CheckConstraint("ck_notificaciones_estado", "estado IN ('Pending','Sent','Failed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_notificaciones_alertas_alerta_id",
                        column: x => x.alerta_id,
                        principalTable: "alertas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notificaciones_archivos_archivo_pdf_id",
                        column: x => x.archivo_pdf_id,
                        principalTable: "archivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alertas_programacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo = table.Column<int>(type: "int", nullable: false),
                    severidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    mensaje = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    taller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    resuelta = table.Column<bool>(type: "bit", nullable: false),
                    creada_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas_programacion", x => x.id);
                    table.ForeignKey(
                        name: "FK_alertas_programacion_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alertas_programacion_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alertas_programacion_talleres_taller_id",
                        column: x => x.taller_id,
                        principalTable: "talleres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avisos_trabajo_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    aviso_id = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    estado_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    sistema = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    subsistema = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    componente = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    prioridad_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    criticidad_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    solicitante_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    evidencia_inicial = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    fecha_deteccion_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    fecha_creacion_usuario_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    clasificacion_falla_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    evaluado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    evaluado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    aprobado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    aprobado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    rechazado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    rechazado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    anulado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    anulado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_anulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    convertido_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    convertido_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avisos_trabajo_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_sql_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_sql_catalogos_trabajo_clasificacion_falla_id",
                        column: x => x.clasificacion_falla_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_sql_catalogos_trabajo_criticidad_id",
                        column: x => x.criticidad_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_sql_catalogos_trabajo_estado_id",
                        column: x => x.estado_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_sql_catalogos_trabajo_prioridad_id",
                        column: x => x.prioridad_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_sql_catalogos_trabajo_tipo_id",
                        column: x => x.tipo_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_sql_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_sql_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avisos_trabajo_sql_unidades_operativas_unidad_operativa_id",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "componentes_unidad_operativa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rol_componente_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fecha_montaje_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    fecha_desmontaje_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    orden_trabajo_montaje_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    orden_trabajo_desmontaje_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    montado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    motivo_montaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    desmontado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    motivo_desmontaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    rol_critico_codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_componentes_unidad_operativa", x => x.id);
                    table.CheckConstraint("ck_componentes_unidad_fechas", "fecha_desmontaje_utc IS NULL OR fecha_desmontaje_utc >= fecha_montaje_utc");
                    table.CheckConstraint("ck_componentes_unidad_rol_critico", "rol_critico_codigo IS NULL OR rol_critico_codigo IN ('FABRICA','CHASIS')");
                    table.ForeignKey(
                        name: "FK_componentes_unidad_operativa_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_componentes_unidad_operativa_ordenes_trabajo_sql_orden_trabajo_desmontaje_id",
                        column: x => x.orden_trabajo_desmontaje_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_componentes_unidad_operativa_ordenes_trabajo_sql_orden_trabajo_montaje_id",
                        column: x => x.orden_trabajo_montaje_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_componentes_unidad_operativa_roles_componente_unidad_rol_componente_id",
                        column: x => x.rol_componente_id,
                        principalTable: "roles_componente_unidad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_componentes_unidad_operativa_unidades_operativas_unidad_operativa_id",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluaciones_preventivas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    plan_preventivo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado = table.Column<int>(type: "int", nullable: false),
                    evaluado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    horas_actuales = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    km_actuales = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    evaluado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluaciones_preventivas", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluaciones_preventivas_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluaciones_preventivas_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluaciones_preventivas_planes_preventivos_sql_plan_preventivo_id",
                        column: x => x.plan_preventivo_id,
                        principalTable: "planes_preventivos_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eventos_disponibilidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    asignacion_contrato_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    causa = table.Column<int>(type: "int", nullable: false),
                    inicio_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    fin_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    puede_utilizarse = table.Column<bool>(type: "bit", nullable: false),
                    atribuible_mantenimiento = table.Column<bool>(type: "bit", nullable: false),
                    comentario = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_disponibilidad", x => x.id);
                    table.CheckConstraint("ck_eventos_disponibilidad_fechas", "fin_utc IS NULL OR fin_utc >= inicio_utc");
                    table.CheckConstraint("ck_eventos_disponibilidad_objetivo", "activo_id IS NOT NULL OR unidad_operativa_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_eventos_disponibilidad_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eventos_disponibilidad_contrato_disponibilidad_objetivos_asignacion_contrato_id",
                        column: x => x.asignacion_contrato_id,
                        principalTable: "contrato_disponibilidad_objetivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eventos_disponibilidad_contratos_disponibilidad_contrato_id",
                        column: x => x.contrato_id,
                        principalTable: "contratos_disponibilidad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eventos_disponibilidad_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eventos_disponibilidad_unidades_operativas_unidad_operativa_id",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "historial_preventivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    plan_preventivo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado_anterior = table.Column<int>(type: "int", nullable: false),
                    estado_nuevo = table.Column<int>(type: "int", nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historial_preventivo", x => x.id);
                    table.ForeignKey(
                        name: "FK_historial_preventivo_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historial_preventivo_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historial_preventivo_planes_preventivos_sql_plan_preventivo_id",
                        column: x => x.plan_preventivo_id,
                        principalTable: "planes_preventivos_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lecturas_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fecha_lectura_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    origen = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    operacion_lectura_unidad_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    es_sincronizacion_composicion = table.Column<bool>(type: "bit", nullable: false),
                    registrado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    evidencia_referencia = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    es_correccion = table.Column<bool>(type: "bit", nullable: false),
                    lectura_corregida_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    motivo_correccion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    autorizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    es_anomala = table.Column<bool>(type: "bit", nullable: false),
                    mensaje_validacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecturas_activo", x => x.id);
                    table.CheckConstraint("ck_lecturas_activo_origen", "origen IN ('MANUAL','ORDEN_TRABAJO','IMPORTACION','SAP','TELEMETRIA')");
                    table.CheckConstraint("ck_lecturas_activo_valor", "valor >= 0");
                    table.ForeignKey(
                        name: "FK_lecturas_activo_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lecturas_activo_lecturas_activo_lectura_corregida_id",
                        column: x => x.lectura_corregida_id,
                        principalTable: "lecturas_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lecturas_activo_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lecturas_activo_unidades_operativas_unidad_operativa_id",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orden_trabajo_activos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    activo_codigo_snapshot = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    activo_nombre_snapshot = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    agregado_en_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    agregado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orden_trabajo_activos", x => x.id);
                    table.CheckConstraint("ck_orden_trabajo_activos_rol", "rol IN ('PRINCIPAL','AFECTADO','MONTAJE','DESMONTAJE')");
                    table.ForeignKey(
                        name: "FK_orden_trabajo_activos_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orden_trabajo_activos_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ot_estado_historial_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado_anterior_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado_nuevo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_estado_historial_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_ot_estado_historial_sql_catalogos_trabajo_estado_anterior_id",
                        column: x => x.estado_anterior_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_estado_historial_sql_catalogos_trabajo_estado_nuevo_id",
                        column: x => x.estado_nuevo_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_estado_historial_sql_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ot_firmas_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    firmante_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    archivo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    firmado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    comentario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    contenido_hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    version_contenido = table.Column<int>(type: "int", nullable: false),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    invalidada_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    invalidada_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_invalidacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_firmas_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_ot_firmas_sql_archivos_archivo_id",
                        column: x => x.archivo_id,
                        principalTable: "archivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_firmas_sql_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_firmas_sql_usuarios_firmante_usuario_id",
                        column: x => x.firmante_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_firmas_sql_usuarios_invalidada_por_usuario_id",
                        column: x => x.invalidada_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ot_tecnicos_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tecnico_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tecnico_nombre_snapshot = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    asignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    asignado_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    desasignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    desasignado_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    motivo_desasignacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "programaciones_ot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    taller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inicio_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    fin_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    hh_estimadas = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    tecnico_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    estado = table.Column<int>(type: "int", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programaciones_ot", x => x.id);
                    table.CheckConstraint("ck_programaciones_ot_fechas", "fin_utc > inicio_utc");
                    table.CheckConstraint("ck_programaciones_ot_hh", "hh_estimadas > 0");
                    table.ForeignKey(
                        name: "FK_programaciones_ot_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_programaciones_ot_talleres_taller_id",
                        column: x => x.taller_id,
                        principalTable: "talleres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reservas_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cantidad_solicitada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_reservada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_entregada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_liberada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    orden_trabajo_numero = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    solicitante = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    motivo_anulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    entregado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    liberado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "solicitudes_repuestos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero_solicitud = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    tipo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    origen = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    bodega_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    solicitante_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    solicitado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    descripcion_tecnica = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    unidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    foto_referencia = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    codigo_tarea = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    decision_stock = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    aprobador_mantenimiento_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    aprobado_mantenimiento_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    aprobador_bodega_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    aprobado_bodega_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    rechazado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    rechazado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    recibido_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    recibido_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    convertido_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    convertido_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cerrado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "tareas_ot_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    codigo_tarea = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    titulo = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    criterio_aceptacion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    estado_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    origen = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    horas_estimadas = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    inicio_programado_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    fin_programado_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    inicio_real_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    completada_tecnico_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    completada_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    aprobada_supervisor_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    aprobada_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    observada_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    observada_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    motivo_observacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    cancelada_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cancelada_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    motivo_cancelacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    plantilla_preventiva_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    item_plantilla_preventiva_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    plantilla_preventiva_version_snapshot = table.Column<int>(type: "int", nullable: true),
                    obligatoria_preventiva = table.Column<bool>(type: "bit", nullable: false),
                    requiere_evidencia = table.Column<bool>(type: "bit", nullable: false),
                    requiere_hh = table.Column<bool>(type: "bit", nullable: false),
                    checklist_obligatorio = table.Column<bool>(type: "bit", nullable: false),
                    observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tareas_ot_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_tareas_ot_sql_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tareas_ot_aprobada_por",
                        column: x => x.aprobada_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tareas_ot_cancelada_por",
                        column: x => x.cancelada_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tareas_ot_completada_por",
                        column: x => x.completada_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tareas_ot_estado",
                        column: x => x.estado_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tareas_ot_observada_por",
                        column: x => x.observada_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vigencias_ubicacion_fisica_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo_ubicacion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    taller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    vigencia_desde_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    vigencia_hasta_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    registrado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vigencias_ubicacion_fisica_activo", x => x.id);
                    table.CheckConstraint("ck_vigencias_ubicacion_fisica_fechas", "vigencia_hasta_utc IS NULL OR vigencia_hasta_utc > vigencia_desde_utc");
                    table.CheckConstraint("ck_vigencias_ubicacion_fisica_tipo", "(tipo_ubicacion = 'FAENA' AND faena_id IS NOT NULL AND taller_id IS NULL) OR (tipo_ubicacion = 'TALLER' AND taller_id IS NOT NULL AND faena_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_vigencias_ubicacion_fisica_activo_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vigencias_ubicacion_fisica_activo_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vigencias_ubicacion_fisica_activo_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vigencias_ubicacion_fisica_activo_talleres_taller_id",
                        column: x => x.taller_id,
                        principalTable: "talleres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vigencias_ubicacion_fisica_activo_unidades_operativas_unidad_operativa_id",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vigencias_ubicacion_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    vigencia_desde_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    vigencia_hasta_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    traslado_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vigencias_ubicacion_activo", x => x.id);
                    table.CheckConstraint("ck_vigencias_ubicacion_activo_fechas", "vigencia_hasta_utc IS NULL OR vigencia_hasta_utc > vigencia_desde_utc");
                    table.ForeignKey(
                        name: "FK_vigencias_ubicacion_activo_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vigencias_ubicacion_activo_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vigencias_ubicacion_activo_traslados_activo_traslado_id",
                        column: x => x.traslado_id,
                        principalTable: "traslados_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documento_activos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    documento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    asignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    asignado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    desasignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    desasignado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    motivo_desasignacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_activos", x => x.id);
                    table.ForeignKey(
                        name: "FK_documento_activos_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documento_activos_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documento_faenas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    documento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    asignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    asignado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    desasignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    desasignado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    motivo_desasignacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_faenas", x => x.id);
                    table.ForeignKey(
                        name: "FK_documento_faenas_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documento_faenas_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documento_ordenes_trabajo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    documento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    asignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    asignado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    desasignado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    desasignado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    motivo_desasignacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_ordenes_trabajo", x => x.id);
                    table.ForeignKey(
                        name: "FK_documento_ordenes_trabajo_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documento_ordenes_trabajo_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "versiones_documento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    documento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero_version = table.Column<int>(type: "int", nullable: false),
                    codigo_version = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    archivo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fecha_carga_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    cargado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    fecha_emision = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    estado_validacion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    validado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    validado_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    rechazado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    rechazado_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    reemplaza_version_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    responsable_correccion_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    estado_correccion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    observacion_correccion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ciclo_correccion_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_versiones_documento", x => x.id);
                    table.ForeignKey(
                        name: "FK_versiones_documento_archivos_archivo_id",
                        column: x => x.archivo_id,
                        principalTable: "archivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_versiones_documento_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_versiones_documento_versiones_documento_reemplaza_version_id",
                        column: x => x.reemplaza_version_id,
                        principalTable: "versiones_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notificacion_destinatarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    notificacion_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    rol_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    destino = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    estado_entrega = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificacion_destinatarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_notificacion_destinatarios_notificaciones_notificacion_id",
                        column: x => x.notificacion_id,
                        principalTable: "notificaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notificacion_destinatarios_roles_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notificacion_destinatarios_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notificacion_intentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    notificacion_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero_intento = table.Column<int>(type: "int", nullable: false),
                    intentado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    exitoso = table.Column<bool>(type: "bit", nullable: false),
                    proveedor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificacion_intentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_notificacion_intentos_notificaciones_notificacion_id",
                        column: x => x.notificacion_id,
                        principalTable: "notificaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dependencias_programacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    programacion_predecesora_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    programacion_sucesora_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dependencias_programacion", x => x.id);
                    table.CheckConstraint("ck_dependencias_programacion_distintas", "programacion_predecesora_id <> programacion_sucesora_id");
                    table.ForeignKey(
                        name: "FK_dependencias_programacion_programaciones_ot_programacion_predecesora_id",
                        column: x => x.programacion_predecesora_id,
                        principalTable: "programaciones_ot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dependencias_programacion_programaciones_ot_programacion_sucesora_id",
                        column: x => x.programacion_sucesora_id,
                        principalTable: "programaciones_ot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimientos_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero_movimiento = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    tipo_movimiento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    bodega_origen_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    bodega_destino_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reserva_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    transferencia_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    tipo_referencia = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    referencia_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    fisico_anterior = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    fisico_nuevo = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    reservado_anterior = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    reservado_nuevo = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    anulado = table.Column<bool>(type: "bit", nullable: false),
                    movimiento_reverso_de_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "solicitud_repuesto_historial",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    solicitud_repuesto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado_anterior = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    estado_nuevo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitud_repuesto_historial", x => x.id);
                    table.ForeignKey(
                        name: "FK_solicitud_repuesto_historial_solicitudes_repuestos_solicitud_repuesto_id",
                        column: x => x.solicitud_repuesto_id,
                        principalTable: "solicitudes_repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "solicitud_repuesto_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    solicitud_repuesto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reserva_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    repuesto_maestro_codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    cantidad_solicitada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_aprobada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_reservada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_entregada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cantidad_devuelta = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    unidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    movimiento_entrega_numero = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                        name: "FK_solicitud_repuesto_items_solicitudes_repuestos_solicitud_repuesto_id",
                        column: x => x.solicitud_repuesto_id,
                        principalTable: "solicitudes_repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "solicitudes_abastecimiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero_solicitud = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    estado = table.Column<int>(type: "int", nullable: false),
                    solicitud_material_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    faena_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    bodega_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    motivo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    solicitada_tecnica_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    aprobada_mantenimiento_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    enviada_abastecimiento_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitudes_abastecimiento", x => x.id);
                    table.ForeignKey(
                        name: "FK_solicitudes_abastecimiento_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitudes_abastecimiento_bodegas_bodega_id",
                        column: x => x.bodega_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitudes_abastecimiento_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitudes_abastecimiento_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitudes_abastecimiento_solicitudes_repuestos_solicitud_material_id",
                        column: x => x.solicitud_material_id,
                        principalTable: "solicitudes_repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ot_evidencias_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    archivo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    tipo_evidencia_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    es_foto = table.Column<bool>(type: "bit", nullable: false),
                    es_obligatoria = table.Column<bool>(type: "bit", nullable: false),
                    cubre_evidencia_obligatoria = table.Column<bool>(type: "bit", nullable: false),
                    proveedor = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    uri_externa = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    clave_externa = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ruta_local = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    offline_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    estado_sync = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    subido_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    subido_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    capturada_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    anulado_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    anulado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_anulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_evidencias_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_ot_evidencias_sql_archivos_archivo_id",
                        column: x => x.archivo_id,
                        principalTable: "archivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_evidencias_sql_catalogos_trabajo_tipo_evidencia_id",
                        column: x => x.tipo_evidencia_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_evidencias_sql_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_evidencias_sql_tareas_ot_sql_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas_ot_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_evidencias_sql_usuarios_anulado_por_usuario_id",
                        column: x => x.anulado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_evidencias_sql_usuarios_subido_por_usuario_id",
                        column: x => x.subido_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ot_hh_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tecnico_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    horas = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    fecha_trabajo_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    hora_inicio_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    hora_termino_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    registrado_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    comentario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    validado_supervisor = table.Column<bool>(type: "bit", nullable: false),
                    validado_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    validado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    anulado_por_usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    anulado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    motivo_anulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_hh_sql", x => x.id);
                    table.CheckConstraint("ck_ot_hh_sql_horas", "horas > 0");
                    table.ForeignKey(
                        name: "FK_ot_hh_sql_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_hh_sql_tareas_ot_sql_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas_ot_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_hh_sql_usuarios_anulado_por_usuario_id",
                        column: x => x.anulado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_hh_sql_usuarios_registrado_por_usuario_id",
                        column: x => x.registrado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_hh_sql_usuarios_tecnico_usuario_id",
                        column: x => x.tecnico_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_hh_sql_usuarios_validado_por_usuario_id",
                        column: x => x.validado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ot_repuestos_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    repuesto_codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    unidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    bodega_codigo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    estado_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cantidad_utilizada = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    cantidad_devuelta = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_repuestos_sql", x => x.id);
                    table.CheckConstraint("ck_ot_repuestos_sql_cantidades", "cantidad > 0 AND cantidad_utilizada >= 0 AND cantidad_devuelta >= 0");
                    table.ForeignKey(
                        name: "FK_ot_repuestos_sql_catalogos_trabajo_estado_id",
                        column: x => x.estado_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_repuestos_sql_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_repuestos_sql_tareas_ot_sql_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas_ot_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tareas_ot_estado_historial_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado_anterior_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    estado_nuevo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tareas_ot_estado_historial_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_tareas_ot_estado_historial_sql_catalogos_trabajo_estado_anterior_id",
                        column: x => x.estado_anterior_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tareas_ot_estado_historial_sql_catalogos_trabajo_estado_nuevo_id",
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

            migrationBuilder.CreateTable(
                name: "detalles_ot_documental",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    activo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    matriz_version_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    matriz_detalle_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    documento_origen_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    version_documento_origen_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    tipo_documental_codigo_snapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    tipo_documental_nombre_snapshot = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    faena_codigo_snapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    obligatorio_snapshot = table.Column<bool>(type: "bit", nullable: false),
                    critico_snapshot = table.Column<bool>(type: "bit", nullable: false),
                    bloquea_disponibilidad_snapshot = table.Column<bool>(type: "bit", nullable: false),
                    requiere_fecha_vencimiento_snapshot = table.Column<bool>(type: "bit", nullable: false),
                    dias_anticipacion_snapshot = table.Column<int>(type: "int", nullable: false),
                    reutilizable_entre_faenas_snapshot = table.Column<bool>(type: "bit", nullable: false),
                    clave_ciclo = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    aplicable = table.Column<bool>(type: "bit", nullable: false),
                    observacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    completado_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalles_ot_documental", x => x.id);
                    table.CheckConstraint("ck_detalles_ot_documental_estado", "estado IN ('PENDIENTE','CARGADO','PENDIENTE_CARGA','PENDIENTE_VALIDACION','VALIDADO','VIGENTE','POR_VENCER','RECHAZADO','VENCIDO','REEMPLAZADO','ANULADO','NO_APLICA')");
                    table.ForeignKey(
                        name: "FK_detalles_ot_documental_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalles_ot_documental_detalles_matriz_requisitos_documentales_matriz_detalle_id",
                        column: x => x.matriz_detalle_id,
                        principalTable: "detalles_matriz_requisitos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalles_ot_documental_documentos_documento_origen_id",
                        column: x => x.documento_origen_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalles_ot_documental_matrices_requisitos_documentales_matriz_version_id",
                        column: x => x.matriz_version_id,
                        principalTable: "matrices_requisitos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalles_ot_documental_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalles_ot_documental_versiones_documento_version_documento_origen_id",
                        column: x => x.version_documento_origen_id,
                        principalTable: "versiones_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "costos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    moneda = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FaenaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SparePartId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StockMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    contrato_codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    proveedor_rut = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    cantidad = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    costo_unitario = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    documento_url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "detalle_solicitud_abastecimiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    solicitud_abastecimiento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    numero_solicitud_externa = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    cantidad_solicitada = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    cantidad_recibida = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    cantidad_entregada = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    unidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    costo_estimado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    moneda = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    documento_respaldo_url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_solicitud_abastecimiento", x => x.id);
                    table.CheckConstraint("ck_detalle_solicitud_abastecimiento_cantidades", "cantidad_solicitada > 0 AND cantidad_recibida >= 0 AND cantidad_entregada >= 0 AND cantidad_entregada <= cantidad_recibida");
                    table.ForeignKey(
                        name: "FK_detalle_solicitud_abastecimiento_repuestos_repuesto_id",
                        column: x => x.repuesto_id,
                        principalTable: "repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalle_solicitud_abastecimiento_solicitudes_abastecimiento_solicitud_abastecimiento_id",
                        column: x => x.solicitud_abastecimiento_id,
                        principalTable: "solicitudes_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_compra",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    numero_oc = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    solicitud_abastecimiento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fecha_oc_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    fecha_comprometida_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    costo_oc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    moneda = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    documento_oc_url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordenes_compra", x => x.id);
                    table.CheckConstraint("ck_ordenes_compra_fechas", "fecha_comprometida_utc >= fecha_oc_utc");
                    table.ForeignKey(
                        name: "FK_ordenes_compra_proveedores_proveedor_id",
                        column: x => x.proveedor_id,
                        principalTable: "proveedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordenes_compra_solicitudes_abastecimiento_solicitud_abastecimiento_id",
                        column: x => x.solicitud_abastecimiento_id,
                        principalTable: "solicitudes_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ot_checklists_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    item_plantilla_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    texto_item = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    obligatorio = table.Column<bool>(type: "bit", nullable: false),
                    completado = table.Column<bool>(type: "bit", nullable: false),
                    completado_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    completado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    tipo_respuesta_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    respuesta = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    valor_numerico = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    texto_libre = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    evidencia_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    firma_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    requiere_foto = table.Column<bool>(type: "bit", nullable: false),
                    requiere_archivo = table.Column<bool>(type: "bit", nullable: false),
                    requiere_firma = table.Column<bool>(type: "bit", nullable: false),
                    vigente = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_checklists_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_ot_checklists_sql_catalogos_trabajo_tipo_respuesta_id",
                        column: x => x.tipo_respuesta_id,
                        principalTable: "catalogos_trabajo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_checklists_sql_items_plantilla_checklist_item_plantilla_id",
                        column: x => x.item_plantilla_id,
                        principalTable: "items_plantilla_checklist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_checklists_sql_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_checklists_sql_ot_evidencias_sql_evidencia_id",
                        column: x => x.evidencia_id,
                        principalTable: "ot_evidencias_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_checklists_sql_ot_firmas_sql_firma_id",
                        column: x => x.firma_id,
                        principalTable: "ot_firmas_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_checklists_sql_plantillas_checklist_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "plantillas_checklist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_checklists_sql_tareas_ot_sql_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas_ot_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "detalle_orden_compra",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_compra_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    detalle_solicitud_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cantidad = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    costo_unitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_orden_compra", x => x.id);
                    table.CheckConstraint("ck_detalle_orden_compra_cantidad", "cantidad > 0");
                    table.ForeignKey(
                        name: "FK_detalle_orden_compra_detalle_solicitud_abastecimiento_detalle_solicitud_id",
                        column: x => x.detalle_solicitud_id,
                        principalTable: "detalle_solicitud_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalle_orden_compra_ordenes_compra_orden_compra_id",
                        column: x => x.orden_compra_id,
                        principalTable: "ordenes_compra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recepciones_abastecimiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    solicitud_abastecimiento_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    orden_compra_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    bodega_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fecha_recepcion_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    despacho_directo_ot = table.Column<bool>(type: "bit", nullable: false),
                    movimiento_recepcion_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    movimiento_entrega_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    costo_real = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    documento_recepcion_url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    documento_entrega_url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recepciones_abastecimiento", x => x.id);
                    table.ForeignKey(
                        name: "FK_recepciones_abastecimiento_bodegas_bodega_id",
                        column: x => x.bodega_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recepciones_abastecimiento_movimientos_stock_movimiento_entrega_id",
                        column: x => x.movimiento_entrega_id,
                        principalTable: "movimientos_stock",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recepciones_abastecimiento_movimientos_stock_movimiento_recepcion_id",
                        column: x => x.movimiento_recepcion_id,
                        principalTable: "movimientos_stock",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recepciones_abastecimiento_ordenes_compra_orden_compra_id",
                        column: x => x.orden_compra_id,
                        principalTable: "ordenes_compra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recepciones_abastecimiento_solicitudes_abastecimiento_solicitud_abastecimiento_id",
                        column: x => x.solicitud_abastecimiento_id,
                        principalTable: "solicitudes_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "detalle_recepcion_abastecimiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    recepcion_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    detalle_solicitud_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cantidad_recibida = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    cantidad_entregada = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_recepcion_abastecimiento", x => x.id);
                    table.CheckConstraint("ck_detalle_recepcion_abastecimiento_cantidades", "cantidad_recibida >= 0 AND cantidad_entregada >= 0 AND cantidad_entregada <= cantidad_recibida");
                    table.ForeignKey(
                        name: "FK_detalle_recepcion_abastecimiento_detalle_solicitud_abastecimiento_detalle_solicitud_id",
                        column: x => x.detalle_solicitud_id,
                        principalTable: "detalle_solicitud_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalle_recepcion_abastecimiento_recepciones_abastecimiento_recepcion_id",
                        column: x => x.recepcion_id,
                        principalTable: "recepciones_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activos_codigo",
                table: "activos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activos_estado_operacional_id",
                table: "activos",
                column: "estado_operacional_id");

            migrationBuilder.CreateIndex(
                name: "IX_activos_faena_id",
                table: "activos",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_activos_familia_equipo_id",
                table: "activos",
                column: "familia_equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_activos_tipo_activo_id",
                table: "activos",
                column: "tipo_activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_alcances_plan_preventivo_activo_id",
                table: "alcances_plan_preventivo",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_alcances_plan_preventivo_familia_equipo_id",
                table: "alcances_plan_preventivo",
                column: "familia_equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_alcances_plan_preventivo_plan_preventivo_id",
                table: "alcances_plan_preventivo",
                column: "plan_preventivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_alcances_plan_preventivo_tipo_activo_id",
                table: "alcances_plan_preventivo",
                column: "tipo_activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_alcances_plan_preventivo_unidad_operativa_id",
                table: "alcances_plan_preventivo",
                column: "unidad_operativa_id");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_archivo_pdf_id",
                table: "alertas",
                column: "archivo_pdf_id");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_estado_severidad",
                table: "alertas",
                columns: new[] { "estado", "severidad" });

            migrationBuilder.CreateIndex(
                name: "IX_alertas_faena_id",
                table: "alertas",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_regla_alerta_id_clave_deduplicacion_activa",
                table: "alertas",
                columns: new[] { "regla_alerta_id", "clave_deduplicacion", "activa" },
                unique: true,
                filter: "activa = 1");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_tipo_entidad_entidad_id",
                table: "alertas",
                columns: new[] { "tipo_entidad", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "IX_alertas_programacion_faena_id",
                table: "alertas_programacion",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_programacion_orden_trabajo_id",
                table: "alertas_programacion",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_programacion_resuelta_creada_at_utc",
                table: "alertas_programacion",
                columns: new[] { "resuelta", "creada_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_alertas_programacion_taller_id",
                table: "alertas_programacion",
                column: "taller_id");

            migrationBuilder.CreateIndex(
                name: "IX_alias_identificador_activo_activo_id_tipo_identificador_vigencia_desde_utc",
                table: "alias_identificador_activo",
                columns: new[] { "activo_id", "tipo_identificador", "vigencia_desde_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_alias_identificador_activo_ambito_valor_normalizado",
                table: "alias_identificador_activo",
                columns: new[] { "ambito", "valor_normalizado" },
                unique: true,
                filter: "vigencia_hasta_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_alias_identificador_activo_reemplazado_por_alias_id",
                table: "alias_identificador_activo",
                column: "reemplazado_por_alias_id");

            migrationBuilder.CreateIndex(
                name: "IX_archivos_checksum",
                table: "archivos",
                column: "checksum");

            migrationBuilder.CreateIndex(
                name: "IX_archivos_created_at_utc",
                table: "archivos",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_archivos_file_key",
                table: "archivos",
                column: "file_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_archivos_proveedor",
                table: "archivos",
                column: "proveedor");

            migrationBuilder.CreateIndex(
                name: "IX_archivos_tipo_entidad_entidad_id_eliminado",
                table: "archivos",
                columns: new[] { "tipo_entidad", "entidad_id", "eliminado" });

            migrationBuilder.CreateIndex(
                name: "IX_archivos_uri_logica",
                table: "archivos",
                column: "uri_logica");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_faena_codigo",
                table: "audit_log",
                column: "faena_codigo");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_modulo_entidad",
                table: "audit_log",
                columns: new[] { "modulo", "entidad" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_occurred_at_utc",
                table: "audit_log",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_activo_id",
                table: "avisos_trabajo_sql",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_aviso_id",
                table: "avisos_trabajo_sql",
                column: "aviso_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_clasificacion_falla_id",
                table: "avisos_trabajo_sql",
                column: "clasificacion_falla_id");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_criticidad_id",
                table: "avisos_trabajo_sql",
                column: "criticidad_id");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_estado_id",
                table: "avisos_trabajo_sql",
                column: "estado_id");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_faena_id",
                table: "avisos_trabajo_sql",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_orden_trabajo_id",
                table: "avisos_trabajo_sql",
                column: "orden_trabajo_id",
                unique: true,
                filter: "orden_trabajo_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_prioridad_id",
                table: "avisos_trabajo_sql",
                column: "prioridad_id");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_tipo_id",
                table: "avisos_trabajo_sql",
                column: "tipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_trabajo_sql_unidad_operativa_id",
                table: "avisos_trabajo_sql",
                column: "unidad_operativa_id");

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
                name: "IX_catalogos_trabajo_categoria_codigo",
                table: "catalogos_trabajo",
                columns: new[] { "categoria", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_componentes_unidad_operativa_activo_id",
                table: "componentes_unidad_operativa",
                column: "activo_id",
                unique: true,
                filter: "fecha_desmontaje_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_componentes_unidad_operativa_orden_trabajo_desmontaje_id",
                table: "componentes_unidad_operativa",
                column: "orden_trabajo_desmontaje_id");

            migrationBuilder.CreateIndex(
                name: "IX_componentes_unidad_operativa_orden_trabajo_montaje_id",
                table: "componentes_unidad_operativa",
                column: "orden_trabajo_montaje_id");

            migrationBuilder.CreateIndex(
                name: "IX_componentes_unidad_operativa_rol_componente_id",
                table: "componentes_unidad_operativa",
                column: "rol_componente_id");

            migrationBuilder.CreateIndex(
                name: "IX_componentes_unidad_operativa_unidad_operativa_id_rol_critico_codigo",
                table: "componentes_unidad_operativa",
                columns: new[] { "unidad_operativa_id", "rol_critico_codigo" },
                unique: true,
                filter: "fecha_desmontaje_utc IS NULL AND rol_critico_codigo IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_disponibilidad_objetivos_activo_id",
                table: "contrato_disponibilidad_objetivos",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_disponibilidad_objetivos_contrato_id_activo_id_activo",
                table: "contrato_disponibilidad_objetivos",
                columns: new[] { "contrato_id", "activo_id", "activo" },
                unique: true,
                filter: "activo_id IS NOT NULL AND activo = 1");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_disponibilidad_objetivos_contrato_id_unidad_operativa_id_activo",
                table: "contrato_disponibilidad_objetivos",
                columns: new[] { "contrato_id", "unidad_operativa_id", "activo" },
                unique: true,
                filter: "unidad_operativa_id IS NOT NULL AND activo = 1");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_disponibilidad_objetivos_unidad_operativa_id",
                table: "contrato_disponibilidad_objetivos",
                column: "unidad_operativa_id");

            migrationBuilder.CreateIndex(
                name: "IX_contratos_disponibilidad_codigo",
                table: "contratos_disponibilidad",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contratos_disponibilidad_faena_id",
                table: "contratos_disponibilidad",
                column: "faena_id");

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
                name: "IX_definiciones_atributo_activo_familia_equipo_id",
                table: "definiciones_atributo_activo",
                column: "familia_equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_definiciones_atributo_activo_tipo_activo_id_codigo",
                table: "definiciones_atributo_activo",
                columns: new[] { "tipo_activo_id", "codigo" },
                unique: true,
                filter: "familia_equipo_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_definiciones_atributo_activo_tipo_activo_id_familia_equipo_id_codigo",
                table: "definiciones_atributo_activo",
                columns: new[] { "tipo_activo_id", "familia_equipo_id", "codigo" },
                unique: true,
                filter: "familia_equipo_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dependencias_programacion_programacion_predecesora_id_programacion_sucesora_id",
                table: "dependencias_programacion",
                columns: new[] { "programacion_predecesora_id", "programacion_sucesora_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dependencias_programacion_programacion_sucesora_id",
                table: "dependencias_programacion",
                column: "programacion_sucesora_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalle_orden_compra_detalle_solicitud_id",
                table: "detalle_orden_compra",
                column: "detalle_solicitud_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalle_orden_compra_orden_compra_id_detalle_solicitud_id",
                table: "detalle_orden_compra",
                columns: new[] { "orden_compra_id", "detalle_solicitud_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detalle_recepcion_abastecimiento_detalle_solicitud_id",
                table: "detalle_recepcion_abastecimiento",
                column: "detalle_solicitud_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalle_recepcion_abastecimiento_recepcion_id_detalle_solicitud_id",
                table: "detalle_recepcion_abastecimiento",
                columns: new[] { "recepcion_id", "detalle_solicitud_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detalle_solicitud_abastecimiento_repuesto_id",
                table: "detalle_solicitud_abastecimiento",
                column: "repuesto_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalle_solicitud_abastecimiento_solicitud_abastecimiento_id",
                table: "detalle_solicitud_abastecimiento",
                column: "solicitud_abastecimiento_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalles_matriz_requisitos_documentales_matriz_id_tipo_documental_id",
                table: "detalles_matriz_requisitos_documentales",
                columns: new[] { "matriz_id", "tipo_documental_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detalles_matriz_requisitos_documentales_tipo_documental_id",
                table: "detalles_matriz_requisitos_documentales",
                column: "tipo_documental_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalles_ot_documental_activo_id_matriz_detalle_id_clave_ciclo",
                table: "detalles_ot_documental",
                columns: new[] { "activo_id", "matriz_detalle_id", "clave_ciclo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detalles_ot_documental_documento_origen_id",
                table: "detalles_ot_documental",
                column: "documento_origen_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalles_ot_documental_matriz_detalle_id",
                table: "detalles_ot_documental",
                column: "matriz_detalle_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalles_ot_documental_matriz_version_id",
                table: "detalles_ot_documental",
                column: "matriz_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalles_ot_documental_orden_trabajo_id",
                table: "detalles_ot_documental",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalles_ot_documental_version_documento_origen_id",
                table: "detalles_ot_documental",
                column: "version_documento_origen_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_activos_activo_id",
                table: "documento_activos",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_activos_documento_id_activo_id_vigente",
                table: "documento_activos",
                columns: new[] { "documento_id", "activo_id", "vigente" },
                unique: true,
                filter: "vigente = 1");

            migrationBuilder.CreateIndex(
                name: "IX_documento_faenas_documento_id_faena_id_vigente",
                table: "documento_faenas",
                columns: new[] { "documento_id", "faena_id", "vigente" },
                unique: true,
                filter: "vigente = 1");

            migrationBuilder.CreateIndex(
                name: "IX_documento_faenas_faena_id",
                table: "documento_faenas",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_ordenes_trabajo_documento_id_orden_trabajo_id_vigente",
                table: "documento_ordenes_trabajo",
                columns: new[] { "documento_id", "orden_trabajo_id", "vigente" },
                unique: true,
                filter: "vigente = 1");

            migrationBuilder.CreateIndex(
                name: "IX_documento_ordenes_trabajo_orden_trabajo_id",
                table: "documento_ordenes_trabajo",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_activo_requisito_activo_id_requisito_matriz_item_id",
                table: "documentos",
                columns: new[] { "activo_requisito_activo_id", "requisito_matriz_item_id" },
                unique: true,
                filter: "activo_requisito_activo_id IS NOT NULL AND requisito_matriz_item_id IS NOT NULL AND anulado = 0 AND historico = 0");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_codigo",
                table: "documentos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documentos_estado",
                table: "documentos",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_matriz_requisito_id",
                table: "documentos",
                column: "matriz_requisito_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_reemplaza_documento_id",
                table: "documentos",
                column: "reemplaza_documento_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_reemplazado_por_documento_id",
                table: "documentos",
                column: "reemplazado_por_documento_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_requisito_matriz_item_id",
                table: "documentos",
                column: "requisito_matriz_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_tipo_documental_id",
                table: "documentos",
                column: "tipo_documental_id");

            migrationBuilder.CreateIndex(
                name: "IX_errores_importacion_importacion_id",
                table: "errores_importacion",
                column: "importacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_estados_operacionales_activo_codigo",
                table: "estados_operacionales_activo",
                column: "codigo",
                unique: true);

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
                name: "IX_evaluaciones_preventivas_activo_id",
                table: "evaluaciones_preventivas",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluaciones_preventivas_orden_trabajo_id",
                table: "evaluaciones_preventivas",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluaciones_preventivas_plan_preventivo_id_activo_id_evaluado_at_utc",
                table: "evaluaciones_preventivas",
                columns: new[] { "plan_preventivo_id", "activo_id", "evaluado_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_disponibilidad_activo_id_inicio_utc",
                table: "eventos_disponibilidad",
                columns: new[] { "activo_id", "inicio_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_disponibilidad_asignacion_contrato_id",
                table: "eventos_disponibilidad",
                column: "asignacion_contrato_id");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_disponibilidad_contrato_id_inicio_utc",
                table: "eventos_disponibilidad",
                columns: new[] { "contrato_id", "inicio_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_disponibilidad_orden_trabajo_id",
                table: "eventos_disponibilidad",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_disponibilidad_unidad_operativa_id_inicio_utc",
                table: "eventos_disponibilidad",
                columns: new[] { "unidad_operativa_id", "inicio_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_estado_activo_activo_id",
                table: "eventos_estado_activo",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_estado_activo_estado_anterior_id",
                table: "eventos_estado_activo",
                column: "estado_anterior_id");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_estado_activo_estado_nuevo_id",
                table: "eventos_estado_activo",
                column: "estado_nuevo_id");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_importacion_importacion_id_fecha_utc",
                table: "eventos_importacion",
                columns: new[] { "importacion_id", "fecha_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_faenas_codigo",
                table: "faenas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_faenas_responsable_usuario_id",
                table: "faenas",
                column: "responsable_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_familias_equipo_codigo",
                table: "familias_equipo",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_familias_equipo_tipo_activo_id",
                table: "familias_equipo",
                column: "tipo_activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_filas_importacion_importacion_id_numero_fila",
                table: "filas_importacion",
                columns: new[] { "importacion_id", "numero_fila" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historial_preventivo_activo_id",
                table: "historial_preventivo",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_historial_preventivo_orden_trabajo_id",
                table: "historial_preventivo",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_historial_preventivo_plan_preventivo_id_activo_id_fecha_utc",
                table: "historial_preventivo",
                columns: new[] { "plan_preventivo_id", "activo_id", "fecha_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_importaciones_archivo_id",
                table: "importaciones",
                column: "archivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_importaciones_estado_cargado_at_utc",
                table: "importaciones",
                columns: new[] { "estado", "cargado_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_items_plantilla_checklist_plantilla_id_orden",
                table: "items_plantilla_checklist",
                columns: new[] { "plantilla_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_items_plantilla_checklist_tipo_respuesta_id",
                table: "items_plantilla_checklist",
                column: "tipo_respuesta_id");

            migrationBuilder.CreateIndex(
                name: "IX_lecturas_activo_activo_id_fecha_lectura_utc",
                table: "lecturas_activo",
                columns: new[] { "activo_id", "fecha_lectura_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_lecturas_activo_lectura_corregida_id",
                table: "lecturas_activo",
                column: "lectura_corregida_id");

            migrationBuilder.CreateIndex(
                name: "IX_lecturas_activo_operacion_lectura_unidad_id",
                table: "lecturas_activo",
                column: "operacion_lectura_unidad_id");

            migrationBuilder.CreateIndex(
                name: "IX_lecturas_activo_orden_trabajo_id",
                table: "lecturas_activo",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_lecturas_activo_unidad_operativa_id",
                table: "lecturas_activo",
                column: "unidad_operativa_id");

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_codigo_numero_version",
                table: "matrices_requisitos_documentales",
                columns: new[] { "faena_id", "codigo", "numero_version" },
                unique: true,
                filter: "[faena_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_tipo_activo_id",
                table: "matrices_requisitos_documentales",
                columns: new[] { "faena_id", "tipo_activo_id" },
                unique: true,
                filter: "faena_id IS NOT NULL AND familia_equipo_id IS NULL AND estado = 'VIGENTE' AND vigencia_hasta IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_tipo_activo_id_familia_equipo_id",
                table: "matrices_requisitos_documentales",
                columns: new[] { "faena_id", "tipo_activo_id", "familia_equipo_id" },
                unique: true,
                filter: "faena_id IS NOT NULL AND familia_equipo_id IS NOT NULL AND estado = 'VIGENTE' AND vigencia_hasta IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_faena_id_tipo_activo_id_familia_equipo_id_vigencia_desde",
                table: "matrices_requisitos_documentales",
                columns: new[] { "faena_id", "tipo_activo_id", "familia_equipo_id", "vigencia_desde" });

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_familia_equipo_id",
                table: "matrices_requisitos_documentales",
                column: "familia_equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_tipo_activo_id",
                table: "matrices_requisitos_documentales",
                column: "tipo_activo_id");

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
                name: "IX_nodo_tecnico_activos_activo_id",
                table: "nodo_tecnico_activos",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodo_tecnico_activos_nodo_tecnico_id_activo_id",
                table: "nodo_tecnico_activos",
                columns: new[] { "nodo_tecnico_id", "activo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nodo_tecnico_aliases_alias_normalizado",
                table: "nodo_tecnico_aliases",
                column: "alias_normalizado");

            migrationBuilder.CreateIndex(
                name: "IX_nodo_tecnico_aliases_nodo_tecnico_id_alias_normalizado",
                table: "nodo_tecnico_aliases",
                columns: new[] { "nodo_tecnico_id", "alias_normalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nodo_tecnico_familias_familia_equipo_id",
                table: "nodo_tecnico_familias",
                column: "familia_equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodo_tecnico_familias_nodo_tecnico_id_familia_equipo_id",
                table: "nodo_tecnico_familias",
                columns: new[] { "nodo_tecnico_id", "familia_equipo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nodos_tecnicos_codigo",
                table: "nodos_tecnicos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nodos_tecnicos_faena_id",
                table: "nodos_tecnicos",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodos_tecnicos_fusionado_en_nodo_id",
                table: "nodos_tecnicos",
                column: "fusionado_en_nodo_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodos_tecnicos_nivel",
                table: "nodos_tecnicos",
                column: "nivel");

            migrationBuilder.CreateIndex(
                name: "IX_nodos_tecnicos_nodo_padre_id",
                table: "nodos_tecnicos",
                column: "nodo_padre_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodos_tecnicos_nodo_padre_id_nivel_nombre_normalizado",
                table: "nodos_tecnicos",
                columns: new[] { "nodo_padre_id", "nivel", "nombre_normalizado" });

            migrationBuilder.CreateIndex(
                name: "IX_nodos_tecnicos_nombre_normalizado",
                table: "nodos_tecnicos",
                column: "nombre_normalizado");

            migrationBuilder.CreateIndex(
                name: "IX_nodos_tecnicos_obsoleto",
                table: "nodos_tecnicos",
                column: "obsoleto");

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_destinatarios_notificacion_id_destino",
                table: "notificacion_destinatarios",
                columns: new[] { "notificacion_id", "destino" },
                unique: true,
                filter: "[destino] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_destinatarios_rol_id",
                table: "notificacion_destinatarios",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_destinatarios_usuario_id",
                table: "notificacion_destinatarios",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_intentos_notificacion_id_numero_intento",
                table: "notificacion_intentos",
                columns: new[] { "notificacion_id", "numero_intento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_alerta_id",
                table: "notificaciones",
                column: "alerta_id");

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_archivo_pdf_id",
                table: "notificaciones",
                column: "archivo_pdf_id");

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_estado_created_at_utc",
                table: "notificaciones",
                columns: new[] { "estado", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_orden_trabajo_activos_activo_id",
                table: "orden_trabajo_activos",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_orden_trabajo_activos_orden_trabajo_id_activo_id",
                table: "orden_trabajo_activos",
                columns: new[] { "orden_trabajo_id", "activo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orden_trabajo_activos_orden_trabajo_id_rol",
                table: "orden_trabajo_activos",
                columns: new[] { "orden_trabajo_id", "rol" });

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_compra_numero_oc",
                table: "ordenes_compra",
                column: "numero_oc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_compra_proveedor_id",
                table: "ordenes_compra",
                column: "proveedor_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_compra_solicitud_abastecimiento_id",
                table: "ordenes_compra",
                column: "solicitud_abastecimiento_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_activo_id",
                table: "ordenes_trabajo_sql",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_aviso_id",
                table: "ordenes_trabajo_sql",
                column: "aviso_id",
                unique: true,
                filter: "aviso_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_clasificacion_falla_id",
                table: "ordenes_trabajo_sql",
                column: "clasificacion_falla_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_criticidad_id",
                table: "ordenes_trabajo_sql",
                column: "criticidad_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_estado_id",
                table: "ordenes_trabajo_sql",
                column: "estado_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_faena_id",
                table: "ordenes_trabajo_sql",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_matriz_documental_version_id",
                table: "ordenes_trabajo_sql",
                column: "matriz_documental_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_numero_ot",
                table: "ordenes_trabajo_sql",
                column: "numero_ot",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_plantilla_preventiva_id",
                table: "ordenes_trabajo_sql",
                column: "plantilla_preventiva_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_prioridad_id",
                table: "ordenes_trabajo_sql",
                column: "prioridad_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_supervisor_asignado_por_usuario_id",
                table: "ordenes_trabajo_sql",
                column: "supervisor_asignado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_supervisor_usuario_id",
                table: "ordenes_trabajo_sql",
                column: "supervisor_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_tipo_mantenimiento_id",
                table: "ordenes_trabajo_sql",
                column: "tipo_mantenimiento_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_unidad_operativa_id",
                table: "ordenes_trabajo_sql",
                column: "unidad_operativa_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_checklists_sql_evidencia_id",
                table: "ot_checklists_sql",
                column: "evidencia_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_checklists_sql_firma_id",
                table: "ot_checklists_sql",
                column: "firma_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_checklists_sql_item_plantilla_id",
                table: "ot_checklists_sql",
                column: "item_plantilla_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_checklists_sql_orden_trabajo_id",
                table: "ot_checklists_sql",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_checklists_sql_plantilla_id",
                table: "ot_checklists_sql",
                column: "plantilla_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_checklists_sql_tarea_id",
                table: "ot_checklists_sql",
                column: "tarea_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_checklists_sql_tipo_respuesta_id",
                table: "ot_checklists_sql",
                column: "tipo_respuesta_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_estado_historial_sql_estado_anterior_id",
                table: "ot_estado_historial_sql",
                column: "estado_anterior_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_estado_historial_sql_estado_nuevo_id",
                table: "ot_estado_historial_sql",
                column: "estado_nuevo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_estado_historial_sql_orden_trabajo_id_fecha_utc",
                table: "ot_estado_historial_sql",
                columns: new[] { "orden_trabajo_id", "fecha_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_anulado_por_usuario_id",
                table: "ot_evidencias_sql",
                column: "anulado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_archivo_id",
                table: "ot_evidencias_sql",
                column: "archivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_orden_trabajo_id",
                table: "ot_evidencias_sql",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_subido_por_usuario_id",
                table: "ot_evidencias_sql",
                column: "subido_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_tarea_id_vigente",
                table: "ot_evidencias_sql",
                columns: new[] { "tarea_id", "vigente" });

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_tipo_evidencia_id",
                table: "ot_evidencias_sql",
                column: "tipo_evidencia_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_archivo_id",
                table: "ot_firmas_sql",
                column: "archivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_firmante_usuario_id",
                table: "ot_firmas_sql",
                column: "firmante_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_invalidada_por_usuario_id",
                table: "ot_firmas_sql",
                column: "invalidada_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_orden_trabajo_id_firmante_usuario_id_vigente",
                table: "ot_firmas_sql",
                columns: new[] { "orden_trabajo_id", "firmante_usuario_id", "vigente" },
                unique: true,
                filter: "vigente = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_anulado_por_usuario_id",
                table: "ot_hh_sql",
                column: "anulado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_orden_trabajo_id",
                table: "ot_hh_sql",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_registrado_por_usuario_id",
                table: "ot_hh_sql",
                column: "registrado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_tarea_id",
                table: "ot_hh_sql",
                column: "tarea_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_tecnico_usuario_id_fecha_trabajo_utc",
                table: "ot_hh_sql",
                columns: new[] { "tecnico_usuario_id", "fecha_trabajo_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_validado_por_usuario_id",
                table: "ot_hh_sql",
                column: "validado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_repuestos_sql_estado_id",
                table: "ot_repuestos_sql",
                column: "estado_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_repuestos_sql_orden_trabajo_id",
                table: "ot_repuestos_sql",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_repuestos_sql_tarea_id",
                table: "ot_repuestos_sql",
                column: "tarea_id");

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
                filter: "vigente = 1");

            migrationBuilder.CreateIndex(
                name: "IX_permisos_codigo",
                table: "permisos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planes_preventivos_sql_activo_proxima_fecha_utc",
                table: "planes_preventivos_sql",
                columns: new[] { "activo", "proxima_fecha_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_planes_preventivos_sql_codigo",
                table: "planes_preventivos_sql",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planes_preventivos_sql_plantilla_checklist_id",
                table: "planes_preventivos_sql",
                column: "plantilla_checklist_id");

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_checklist_codigo",
                table: "plantillas_checklist",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_pdf_archivo_id",
                table: "plantillas_pdf",
                column: "archivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_pdf_codigo",
                table: "plantillas_pdf",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_pdf_tipo_evento_activo",
                table: "plantillas_pdf",
                columns: new[] { "tipo_evento", "activo" });

            migrationBuilder.CreateIndex(
                name: "IX_programaciones_ot_orden_trabajo_id",
                table: "programaciones_ot",
                column: "orden_trabajo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_programaciones_ot_taller_id_inicio_utc",
                table: "programaciones_ot",
                columns: new[] { "taller_id", "inicio_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_proveedores_rut",
                table: "proveedores",
                column: "rut",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recepciones_abastecimiento_bodega_id",
                table: "recepciones_abastecimiento",
                column: "bodega_id");

            migrationBuilder.CreateIndex(
                name: "IX_recepciones_abastecimiento_movimiento_entrega_id",
                table: "recepciones_abastecimiento",
                column: "movimiento_entrega_id");

            migrationBuilder.CreateIndex(
                name: "IX_recepciones_abastecimiento_movimiento_recepcion_id",
                table: "recepciones_abastecimiento",
                column: "movimiento_recepcion_id");

            migrationBuilder.CreateIndex(
                name: "IX_recepciones_abastecimiento_orden_compra_id",
                table: "recepciones_abastecimiento",
                column: "orden_compra_id");

            migrationBuilder.CreateIndex(
                name: "IX_recepciones_abastecimiento_solicitud_abastecimiento_id_fecha_recepcion_utc",
                table: "recepciones_abastecimiento",
                columns: new[] { "solicitud_abastecimiento_id", "fecha_recepcion_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_regla_alerta_destinatarios_regla_alerta_id_destino_canal",
                table: "regla_alerta_destinatarios",
                columns: new[] { "regla_alerta_id", "destino", "canal" },
                unique: true,
                filter: "[destino] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_regla_alerta_destinatarios_rol_id",
                table: "regla_alerta_destinatarios",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_regla_alerta_destinatarios_usuario_id",
                table: "regla_alerta_destinatarios",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_reglas_alerta_activa_severidad",
                table: "reglas_alerta",
                columns: new[] { "activa", "severidad" });

            migrationBuilder.CreateIndex(
                name: "IX_reglas_alerta_codigo",
                table: "reglas_alerta",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reglas_alerta_faena_id",
                table: "reglas_alerta",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_reglas_alerta_plantilla_id",
                table: "reglas_alerta",
                column: "plantilla_id");

            migrationBuilder.CreateIndex(
                name: "IX_reglas_alerta_tipo_evento",
                table: "reglas_alerta",
                column: "tipo_evento");

            migrationBuilder.CreateIndex(
                name: "IX_reglas_composicion_unidad_rol_componente_id",
                table: "reglas_composicion_unidad",
                column: "rol_componente_id");

            migrationBuilder.CreateIndex(
                name: "IX_reglas_composicion_unidad_tipo_unidad_operativa_id_rol_componente_id",
                table: "reglas_composicion_unidad",
                columns: new[] { "tipo_unidad_operativa_id", "rol_componente_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reglas_composicion_unidad_activos_permitidos_familia_equipo_id",
                table: "reglas_composicion_unidad_activos_permitidos",
                column: "familia_equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_reglas_composicion_unidad_activos_permitidos_regla_composicion_id_tipo_activo_id_familia_equipo_id",
                table: "reglas_composicion_unidad_activos_permitidos",
                columns: new[] { "regla_composicion_id", "tipo_activo_id", "familia_equipo_id" },
                unique: true,
                filter: "[tipo_activo_id] IS NOT NULL AND [familia_equipo_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_reglas_composicion_unidad_activos_permitidos_tipo_activo_id",
                table: "reglas_composicion_unidad_activos_permitidos",
                column: "tipo_activo_id");

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
                name: "IX_requisitos_documentales_tipo_activo_familia_equipo_id",
                table: "requisitos_documentales_tipo_activo",
                column: "familia_equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_requisitos_documentales_tipo_activo_tipo_activo_id_familia_equipo_id_tipo_documental_id",
                table: "requisitos_documentales_tipo_activo",
                columns: new[] { "tipo_activo_id", "familia_equipo_id", "tipo_documental_id" },
                unique: true,
                filter: "[familia_equipo_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_requisitos_documentales_tipo_activo_tipo_documental_id",
                table: "requisitos_documentales_tipo_activo",
                column: "tipo_documental_id");

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
                name: "IX_rol_permisos_permiso_id",
                table: "rol_permisos",
                column: "permiso_id");

            migrationBuilder.CreateIndex(
                name: "IX_rol_permisos_rol_id_permiso_id_vigente",
                table: "rol_permisos",
                columns: new[] { "rol_id", "permiso_id", "vigente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_codigo",
                table: "roles",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_componente_unidad_codigo",
                table: "roles_componente_unidad",
                column: "codigo",
                unique: true);

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
                name: "IX_solicitudes_abastecimiento_activo_id",
                table: "solicitudes_abastecimiento",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_abastecimiento_bodega_id",
                table: "solicitudes_abastecimiento",
                column: "bodega_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_abastecimiento_estado_enviada_abastecimiento_at_utc",
                table: "solicitudes_abastecimiento",
                columns: new[] { "estado", "enviada_abastecimiento_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_abastecimiento_faena_id",
                table: "solicitudes_abastecimiento",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_abastecimiento_numero_solicitud",
                table: "solicitudes_abastecimiento",
                column: "numero_solicitud",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_abastecimiento_orden_trabajo_id",
                table: "solicitudes_abastecimiento",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_abastecimiento_solicitud_material_id",
                table: "solicitudes_abastecimiento",
                column: "solicitud_material_id");

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

            migrationBuilder.CreateIndex(
                name: "IX_stock_bodega_bodega_id",
                table: "stock_bodega",
                column: "bodega_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_bodega_repuesto_id_bodega_id_ubicacion_bodega_id",
                table: "stock_bodega",
                columns: new[] { "repuesto_id", "bodega_id", "ubicacion_bodega_id" },
                unique: true,
                filter: "[ubicacion_bodega_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stock_bodega_ubicacion_bodega_id",
                table: "stock_bodega",
                column: "ubicacion_bodega_id");

            migrationBuilder.CreateIndex(
                name: "IX_talleres_codigo",
                table: "talleres",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_talleres_supervisor_usuario_id",
                table: "talleres",
                column: "supervisor_usuario_id");

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
                name: "IX_tareas_ot_sql_orden_trabajo_id_codigo_tarea",
                table: "tareas_ot_sql",
                columns: new[] { "orden_trabajo_id", "codigo_tarea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tarifas_hh_codigo",
                table: "tarifas_hh",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipos_activo_codigo",
                table: "tipos_activo",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipos_documentales_codigo",
                table: "tipos_documentales",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipos_unidad_operativa_codigo",
                table: "tipos_unidad_operativa",
                column: "codigo",
                unique: true);

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
                name: "IX_traslados_activo_activo_id_fecha_efectiva_utc",
                table: "traslados_activo",
                columns: new[] { "activo_id", "fecha_efectiva_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_traslados_activo_faena_destino_id",
                table: "traslados_activo",
                column: "faena_destino_id");

            migrationBuilder.CreateIndex(
                name: "IX_traslados_activo_faena_origen_id",
                table: "traslados_activo",
                column: "faena_origen_id");

            migrationBuilder.CreateIndex(
                name: "IX_traslados_activo_unidad_operativa_id",
                table: "traslados_activo",
                column: "unidad_operativa_id");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_bodega_bodega_id_codigo",
                table: "ubicaciones_bodega",
                columns: new[] { "bodega_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_codigo",
                table: "ubicaciones_tecnicas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_faena_id",
                table: "ubicaciones_tecnicas",
                column: "faena_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_nombre",
                table: "ubicaciones_tecnicas",
                column: "nombre");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_obsoleto",
                table: "ubicaciones_tecnicas",
                column: "obsoleto");

            migrationBuilder.CreateIndex(
                name: "IX_unidades_operativas_codigo",
                table: "unidades_operativas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidades_operativas_estado_derivado_por_activo_id",
                table: "unidades_operativas",
                column: "estado_derivado_por_activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidades_operativas_estado_operacional_base_id",
                table: "unidades_operativas",
                column: "estado_operacional_base_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidades_operativas_estado_operacional_id",
                table: "unidades_operativas",
                column: "estado_operacional_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidades_operativas_faena_id",
                table: "unidades_operativas",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidades_operativas_tipo_unidad_operativa_id",
                table: "unidades_operativas",
                column: "tipo_unidad_operativa_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_faenas_faena_id",
                table: "usuario_faenas",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_faenas_usuario_id_faena_id",
                table: "usuario_faenas",
                columns: new[] { "usuario_id", "faena_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_roles_rol_id",
                table: "usuario_roles",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_roles_usuario_id_rol_id",
                table: "usuario_roles",
                columns: new[] { "usuario_id", "rol_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_username",
                table: "usuarios",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_valores_atributo_activo_activo_id_definicion_atributo_id",
                table: "valores_atributo_activo",
                columns: new[] { "activo_id", "definicion_atributo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_valores_atributo_activo_definicion_atributo_id",
                table: "valores_atributo_activo",
                column: "definicion_atributo_id");

            migrationBuilder.CreateIndex(
                name: "IX_versiones_documento_archivo_id",
                table: "versiones_documento",
                column: "archivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_versiones_documento_documento_id_numero_version",
                table: "versiones_documento",
                columns: new[] { "documento_id", "numero_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_versiones_documento_documento_id_vigente",
                table: "versiones_documento",
                columns: new[] { "documento_id", "vigente" },
                unique: true,
                filter: "vigente = 1");

            migrationBuilder.CreateIndex(
                name: "IX_versiones_documento_reemplaza_version_id",
                table: "versiones_documento",
                column: "reemplaza_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_vigencias_ubicacion_activo_activo_id",
                table: "vigencias_ubicacion_activo",
                column: "activo_id",
                unique: true,
                filter: "vigencia_hasta_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vigencias_ubicacion_activo_activo_id_vigencia_desde_utc",
                table: "vigencias_ubicacion_activo",
                columns: new[] { "activo_id", "vigencia_desde_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vigencias_ubicacion_activo_faena_id",
                table: "vigencias_ubicacion_activo",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_vigencias_ubicacion_activo_traslado_id",
                table: "vigencias_ubicacion_activo",
                column: "traslado_id");

            migrationBuilder.CreateIndex(
                name: "IX_vigencias_ubicacion_fisica_activo_activo_id",
                table: "vigencias_ubicacion_fisica_activo",
                column: "activo_id",
                unique: true,
                filter: "vigencia_hasta_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vigencias_ubicacion_fisica_activo_activo_id_vigencia_desde_utc",
                table: "vigencias_ubicacion_fisica_activo",
                columns: new[] { "activo_id", "vigencia_desde_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vigencias_ubicacion_fisica_activo_faena_id",
                table: "vigencias_ubicacion_fisica_activo",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_vigencias_ubicacion_fisica_activo_orden_trabajo_id",
                table: "vigencias_ubicacion_fisica_activo",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_vigencias_ubicacion_fisica_activo_taller_id",
                table: "vigencias_ubicacion_fisica_activo",
                column: "taller_id");

            migrationBuilder.CreateIndex(
                name: "IX_vigencias_ubicacion_fisica_activo_unidad_operativa_id",
                table: "vigencias_ubicacion_fisica_activo",
                column: "unidad_operativa_id");
            migrationBuilder.Sql("""
                CREATE SEQUENCE dbo.asset_number_seq AS bigint START WITH 1 INCREMENT BY 1;
                CREATE SEQUENCE dbo.material_request_number_seq AS bigint START WITH 1 INCREMENT BY 1;
                CREATE SEQUENCE dbo.work_notification_number_seq AS bigint START WITH 1 INCREMENT BY 1;
                CREATE SEQUENCE dbo.work_order_number_seq AS bigint START WITH 1 INCREMENT BY 1;
                CREATE SEQUENCE dbo.spare_part_number_seq AS bigint START WITH 1 INCREMENT BY 1;
                CREATE SEQUENCE dbo.stock_movement_number_seq AS bigint START WITH 1 INCREMENT BY 1;
                CREATE SEQUENCE dbo.stock_reservation_number_seq AS bigint START WITH 1 INCREMENT BY 1;
                CREATE SEQUENCE dbo.stock_transfer_number_seq AS bigint START WITH 1 INCREMENT BY 1;

                ALTER TABLE dbo.definiciones_atributo_activo ADD CONSTRAINT ck_definiciones_atributo_opciones_json CHECK (opciones_json IS NULL OR ISJSON(opciones_json) = 1);
                ALTER TABLE dbo.filas_importacion ADD CONSTRAINT ck_filas_importacion_snapshot_json CHECK (ISJSON(snapshot_entrada) = 1);
                ALTER TABLE dbo.archivos ADD CONSTRAINT ck_archivos_metadata_json CHECK (metadata IS NULL OR ISJSON(metadata) = 1);
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.trg_activos_estado_operacional_requires_event
                ON dbo.activos
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN deleted d ON d.id = i.id
                        WHERE (i.estado_operacional_id <> d.estado_operacional_id
                            OR (i.estado_operacional_id IS NULL AND d.estado_operacional_id IS NOT NULL)
                            OR (i.estado_operacional_id IS NOT NULL AND d.estado_operacional_id IS NULL))
                          AND NOT EXISTS (
                              SELECT 1
                              FROM dbo.eventos_estado_activo e
                              INNER JOIN STRING_SPLIT(CONVERT(nvarchar(max), SESSION_CONTEXT(N'cmms.asset_state_event_ids')), N',') correlation
                                  ON e.id = TRY_CONVERT(uniqueidentifier, LTRIM(RTRIM(correlation.value)))
                              WHERE e.activo_id = i.id
                                AND ((e.estado_anterior_id = d.estado_operacional_id) OR (e.estado_anterior_id IS NULL AND d.estado_operacional_id IS NULL))
                                AND e.estado_nuevo_id = i.estado_operacional_id))
                    BEGIN
                        THROW 51001, N'El estado operacional solo puede cambiar mediante un evento de estado persistido.', 1;
                    END
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.trg_activos_faena_requires_transfer
                ON dbo.activos
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN deleted d ON d.id = i.id
                        WHERE (i.faena_id <> d.faena_id
                            OR (i.faena_id IS NULL AND d.faena_id IS NOT NULL)
                            OR (i.faena_id IS NOT NULL AND d.faena_id IS NULL))
                          AND NOT EXISTS (
                              SELECT 1
                              FROM dbo.traslados_activo t
                              INNER JOIN STRING_SPLIT(CONVERT(nvarchar(max), SESSION_CONTEXT(N'cmms.asset_transfer_ids')), N',') correlation
                                  ON t.id = TRY_CONVERT(uniqueidentifier, LTRIM(RTRIM(correlation.value)))
                              WHERE t.activo_id = i.id
                                AND ((t.faena_origen_id = d.faena_id) OR (t.faena_origen_id IS NULL AND d.faena_id IS NULL))
                                AND t.faena_destino_id = i.faena_id))
                    BEGIN
                        THROW 51002, N'La faena solo puede cambiar mediante un traslado persistido.', 1;
                    END
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.trg_componentes_unidad_rol_critico
                ON dbo.componentes_unidad_operativa
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    UPDATE component
                    SET rol_critico_codigo = CASE WHEN role.critico = 1 THEN UPPER(role.codigo) ELSE NULL END
                    FROM dbo.componentes_unidad_operativa component
                    INNER JOIN inserted i ON i.id = component.id
                    INNER JOIN dbo.roles_componente_unidad role ON role.id = component.rol_componente_id;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.trg_valores_atributo_activo_unico
                ON dbo.valores_atributo_activo
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    DECLARE @lockResult int;
                    EXEC @lockResult = sys.sp_getapplock @Resource=N'cmms.dynamic-attribute-unique', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
                    IF @lockResult < 0 THROW 51003, N'No se pudo obtener el bloqueo de atributos dinámicos.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN dbo.definiciones_atributo_activo definition ON definition.id = i.definicion_atributo_id AND definition.es_unico = 1
                        INNER JOIN dbo.valores_atributo_activo value ON value.definicion_atributo_id = i.definicion_atributo_id AND value.activo_id <> i.activo_id
                        WHERE ((value.valor_texto = i.valor_texto) OR (value.valor_texto IS NULL AND i.valor_texto IS NULL))
                          AND ((value.valor_numerico = i.valor_numerico) OR (value.valor_numerico IS NULL AND i.valor_numerico IS NULL))
                          AND ((value.valor_booleano = i.valor_booleano) OR (value.valor_booleano IS NULL AND i.valor_booleano IS NULL))
                          AND ((value.valor_fecha = i.valor_fecha) OR (value.valor_fecha IS NULL AND i.valor_fecha IS NULL)))
                    BEGIN
                        THROW 51004, N'Identificador dinámico duplicado.', 1;
                    END
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.trg_vigencias_ubicacion_activo_sin_solape
                ON dbo.vigencias_ubicacion_activo
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    DECLARE @lockResult int;
                    EXEC @lockResult = sys.sp_getapplock @Resource=N'cmms.asset-location-periods', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
                    IF @lockResult < 0 THROW 51005, N'No se pudo obtener el bloqueo de vigencias de ubicación.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM dbo.vigencias_ubicacion_activo a
                        INNER JOIN dbo.vigencias_ubicacion_activo b ON b.activo_id = a.activo_id AND b.id <> a.id
                        INNER JOIN (SELECT activo_id FROM inserted UNION SELECT activo_id FROM deleted) changed ON changed.activo_id = a.activo_id
                        WHERE a.vigencia_desde_utc < COALESCE(b.vigencia_hasta_utc, CONVERT(datetimeoffset, '9999-12-31T23:59:59.9999999+00:00'))
                          AND b.vigencia_desde_utc < COALESCE(a.vigencia_hasta_utc, CONVERT(datetimeoffset, '9999-12-31T23:59:59.9999999+00:00')))
                    BEGIN
                        THROW 51006, N'Las vigencias de ubicación de un activo no pueden solaparse.', 1;
                    END
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.trg_vigencias_ubicacion_fisica_activo_sin_solape
                ON dbo.vigencias_ubicacion_fisica_activo
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    DECLARE @lockResult int;
                    EXEC @lockResult = sys.sp_getapplock @Resource=N'cmms.asset-physical-location-periods', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
                    IF @lockResult < 0 THROW 51007, N'No se pudo obtener el bloqueo de vigencias físicas.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM dbo.vigencias_ubicacion_fisica_activo a
                        INNER JOIN dbo.vigencias_ubicacion_fisica_activo b ON b.activo_id = a.activo_id AND b.id <> a.id
                        INNER JOIN (SELECT activo_id FROM inserted UNION SELECT activo_id FROM deleted) changed ON changed.activo_id = a.activo_id
                        WHERE a.vigencia_desde_utc < COALESCE(b.vigencia_hasta_utc, CONVERT(datetimeoffset, '9999-12-31T23:59:59.9999999+00:00'))
                          AND b.vigencia_desde_utc < COALESCE(a.vigencia_hasta_utc, CONVERT(datetimeoffset, '9999-12-31T23:59:59.9999999+00:00')))
                    BEGIN
                        THROW 51008, N'Las vigencias físicas de un activo no pueden solaparse.', 1;
                    END
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.trg_matrices_requisitos_sin_solape
                ON dbo.matrices_requisitos_documentales
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    DECLARE @lockResult int;
                    EXEC @lockResult = sys.sp_getapplock @Resource=N'cmms.document-requirement-matrices', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
                    IF @lockResult < 0 THROW 51009, N'No se pudo obtener el bloqueo de matrices documentales.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM dbo.matrices_requisitos_documentales a
                        INNER JOIN dbo.matrices_requisitos_documentales b
                            ON b.id <> a.id
                           AND b.tipo_activo_id = a.tipo_activo_id
                           AND ((b.faena_id = a.faena_id) OR (b.faena_id IS NULL AND a.faena_id IS NULL))
                           AND ((b.familia_equipo_id = a.familia_equipo_id) OR (b.familia_equipo_id IS NULL AND a.familia_equipo_id IS NULL))
                        INNER JOIN (SELECT id FROM inserted UNION SELECT id FROM deleted) changed ON changed.id = a.id OR changed.id = b.id
                        WHERE a.estado = 'VIGENTE' AND b.estado = 'VIGENTE'
                          AND a.vigencia_desde <= COALESCE(b.vigencia_hasta, CONVERT(date, '9999-12-31'))
                          AND b.vigencia_desde <= COALESCE(a.vigencia_hasta, CONVERT(date, '9999-12-31')))
                    BEGIN
                        THROW 51010, N'Las matrices documentales vigentes no pueden solaparse.', 1;
                    END
                END
                """);

            foreach (var protectedTable in new[]
                     {
                         (Table: "activos", Trigger: "trg_no_delete_activos"),
                         (Table: "eventos_estado_activo", Trigger: "trg_no_delete_eventos_estado_activo"),
                         (Table: "traslados_activo", Trigger: "trg_no_delete_traslados_activo"),
                         (Table: "vigencias_ubicacion_activo", Trigger: "trg_no_delete_vigencias_ubicacion_activo"),
                         (Table: "vigencias_ubicacion_fisica_activo", Trigger: "trg_no_delete_vigencias_ubicacion_fisica_activo"),
                         (Table: "documentos", Trigger: "trg_no_delete_documentos"),
                         (Table: "versiones_documento", Trigger: "trg_no_delete_versiones_documento"),
                         (Table: "componentes_unidad_operativa", Trigger: "trg_no_delete_componentes_unidad_operativa"),
                         (Table: "ordenes_trabajo_sql", Trigger: "trg_no_delete_ordenes_trabajo"),
                         (Table: "matrices_requisitos_documentales", Trigger: "trg_no_delete_matrices_requisitos_documentales"),
                         (Table: "detalles_ot_documental", Trigger: "trg_no_delete_detalles_ot_documental")
                     })
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER dbo.[{protectedTable.Trigger}]
                    ON dbo.[{protectedTable.Table}]
                    INSTEAD OF DELETE
                    AS
                    BEGIN
                        THROW 51011, N'No se permiten borrados físicos en historial crítico CMMS.', 1;
                    END
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP SEQUENCE dbo.asset_number_seq;
                DROP SEQUENCE dbo.material_request_number_seq;
                DROP SEQUENCE dbo.work_notification_number_seq;
                DROP SEQUENCE dbo.work_order_number_seq;
                DROP SEQUENCE dbo.spare_part_number_seq;
                DROP SEQUENCE dbo.stock_movement_number_seq;
                DROP SEQUENCE dbo.stock_reservation_number_seq;
                DROP SEQUENCE dbo.stock_transfer_number_seq;
                """);
            migrationBuilder.DropTable(
                name: "alcances_plan_preventivo");

            migrationBuilder.DropTable(
                name: "alertas_programacion");

            migrationBuilder.DropTable(
                name: "alias_identificador_activo");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "avisos_trabajo_sql");

            migrationBuilder.DropTable(
                name: "componentes_unidad_operativa");

            migrationBuilder.DropTable(
                name: "costos");

            migrationBuilder.DropTable(
                name: "dependencias_programacion");

            migrationBuilder.DropTable(
                name: "detalle_orden_compra");

            migrationBuilder.DropTable(
                name: "detalle_recepcion_abastecimiento");

            migrationBuilder.DropTable(
                name: "detalles_ot_documental");

            migrationBuilder.DropTable(
                name: "documento_activos");

            migrationBuilder.DropTable(
                name: "documento_faenas");

            migrationBuilder.DropTable(
                name: "documento_ordenes_trabajo");

            migrationBuilder.DropTable(
                name: "errores_importacion");

            migrationBuilder.DropTable(
                name: "estados_pago");

            migrationBuilder.DropTable(
                name: "evaluaciones_preventivas");

            migrationBuilder.DropTable(
                name: "eventos_disponibilidad");

            migrationBuilder.DropTable(
                name: "eventos_estado_activo");

            migrationBuilder.DropTable(
                name: "eventos_importacion");

            migrationBuilder.DropTable(
                name: "filas_importacion");

            migrationBuilder.DropTable(
                name: "historial_preventivo");

            migrationBuilder.DropTable(
                name: "lecturas_activo");

            migrationBuilder.DropTable(
                name: "nodo_tecnico_activos");

            migrationBuilder.DropTable(
                name: "nodo_tecnico_aliases");

            migrationBuilder.DropTable(
                name: "nodo_tecnico_familias");

            migrationBuilder.DropTable(
                name: "notificacion_destinatarios");

            migrationBuilder.DropTable(
                name: "notificacion_intentos");

            migrationBuilder.DropTable(
                name: "orden_trabajo_activos");

            migrationBuilder.DropTable(
                name: "ot_checklists_sql");

            migrationBuilder.DropTable(
                name: "ot_estado_historial_sql");

            migrationBuilder.DropTable(
                name: "ot_hh_sql");

            migrationBuilder.DropTable(
                name: "ot_repuestos_sql");

            migrationBuilder.DropTable(
                name: "ot_tecnicos_sql");

            migrationBuilder.DropTable(
                name: "regla_alerta_destinatarios");

            migrationBuilder.DropTable(
                name: "reglas_composicion_unidad_activos_permitidos");

            migrationBuilder.DropTable(
                name: "requisitos_documentales_tipo_activo");

            migrationBuilder.DropTable(
                name: "rol_permisos");

            migrationBuilder.DropTable(
                name: "solicitud_repuesto_historial");

            migrationBuilder.DropTable(
                name: "solicitud_repuesto_items");

            migrationBuilder.DropTable(
                name: "stock_bodega");

            migrationBuilder.DropTable(
                name: "tareas_ot_estado_historial_sql");

            migrationBuilder.DropTable(
                name: "tarifas_hh");

            migrationBuilder.DropTable(
                name: "ubicaciones_tecnicas");

            migrationBuilder.DropTable(
                name: "usuario_faenas");

            migrationBuilder.DropTable(
                name: "usuario_roles");

            migrationBuilder.DropTable(
                name: "valores_atributo_activo");

            migrationBuilder.DropTable(
                name: "vigencias_ubicacion_activo");

            migrationBuilder.DropTable(
                name: "vigencias_ubicacion_fisica_activo");

            migrationBuilder.DropTable(
                name: "programaciones_ot");

            migrationBuilder.DropTable(
                name: "detalle_solicitud_abastecimiento");

            migrationBuilder.DropTable(
                name: "recepciones_abastecimiento");

            migrationBuilder.DropTable(
                name: "versiones_documento");

            migrationBuilder.DropTable(
                name: "contrato_disponibilidad_objetivos");

            migrationBuilder.DropTable(
                name: "importaciones");

            migrationBuilder.DropTable(
                name: "planes_preventivos_sql");

            migrationBuilder.DropTable(
                name: "nodos_tecnicos");

            migrationBuilder.DropTable(
                name: "notificaciones");

            migrationBuilder.DropTable(
                name: "items_plantilla_checklist");

            migrationBuilder.DropTable(
                name: "ot_evidencias_sql");

            migrationBuilder.DropTable(
                name: "ot_firmas_sql");

            migrationBuilder.DropTable(
                name: "reglas_composicion_unidad");

            migrationBuilder.DropTable(
                name: "permisos");

            migrationBuilder.DropTable(
                name: "ubicaciones_bodega");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "definiciones_atributo_activo");

            migrationBuilder.DropTable(
                name: "traslados_activo");

            migrationBuilder.DropTable(
                name: "talleres");

            migrationBuilder.DropTable(
                name: "movimientos_stock");

            migrationBuilder.DropTable(
                name: "ordenes_compra");

            migrationBuilder.DropTable(
                name: "documentos");

            migrationBuilder.DropTable(
                name: "contratos_disponibilidad");

            migrationBuilder.DropTable(
                name: "alertas");

            migrationBuilder.DropTable(
                name: "tareas_ot_sql");

            migrationBuilder.DropTable(
                name: "roles_componente_unidad");

            migrationBuilder.DropTable(
                name: "reservas_stock");

            migrationBuilder.DropTable(
                name: "transferencias_stock");

            migrationBuilder.DropTable(
                name: "proveedores");

            migrationBuilder.DropTable(
                name: "solicitudes_abastecimiento");

            migrationBuilder.DropTable(
                name: "detalles_matriz_requisitos_documentales");

            migrationBuilder.DropTable(
                name: "reglas_alerta");

            migrationBuilder.DropTable(
                name: "repuestos");

            migrationBuilder.DropTable(
                name: "solicitudes_repuestos");

            migrationBuilder.DropTable(
                name: "tipos_documentales");

            migrationBuilder.DropTable(
                name: "plantillas_pdf");

            migrationBuilder.DropTable(
                name: "bodegas");

            migrationBuilder.DropTable(
                name: "ordenes_trabajo_sql");

            migrationBuilder.DropTable(
                name: "archivos");

            migrationBuilder.DropTable(
                name: "catalogos_inventario");

            migrationBuilder.DropTable(
                name: "catalogos_trabajo");

            migrationBuilder.DropTable(
                name: "unidades_operativas");

            migrationBuilder.DropTable(
                name: "matrices_requisitos_documentales");

            migrationBuilder.DropTable(
                name: "plantillas_checklist");

            migrationBuilder.DropTable(
                name: "activos");

            migrationBuilder.DropTable(
                name: "tipos_unidad_operativa");

            migrationBuilder.DropTable(
                name: "estados_operacionales_activo");

            migrationBuilder.DropTable(
                name: "faenas");

            migrationBuilder.DropTable(
                name: "familias_equipo");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "tipos_activo");
        }
    }
}
