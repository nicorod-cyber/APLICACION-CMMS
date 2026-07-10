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
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "archivos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_key = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    proveedor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    uri_logica = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ruta_logica = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                constraints: table =>
                {
                    table.PrimaryKey("PK_archivos", x => x.id);
                });

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
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                });

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
                name: "catalogos_trabajo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogos_trabajo", x => x.id);
                });

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
                    table.PrimaryKey("PK_estados_operacionales_activo", x => x.id);
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
                constraints: table =>
                {
                    table.PrimaryKey("PK_faenas", x => x.id);
                });

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
                constraints: table =>
                {
                    table.PrimaryKey("PK_familias_equipo", x => x.id);
                });

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
                constraints: table =>
                {
                    table.PrimaryKey("PK_permisos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plantillas_checklist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    tipo_ot_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    familia_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    plan_preventivo_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tarea_codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    activo_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plantillas_checklist", x => x.id);
                });

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
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

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
                    table.PrimaryKey("PK_tipos_documentales", x => x.id);
                    table.CheckConstraint("ck_tipos_documentales_dias_alerta", "dias_alerta >= 0");
                });

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
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
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
                    table.PrimaryKey("PK_activos", x => x.id);
                    table.CheckConstraint("ck_activos_estado_registro", "estado_registro IN ('vigente','inactivo','anulado','obsoleto','reemplazado','no_vigente')");
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
                });

            migrationBuilder.CreateTable(
                name: "items_plantilla_checklist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    tipo_respuesta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requiere_foto = table.Column<bool>(type: "boolean", nullable: false),
                    requiere_archivo = table.Column<bool>(type: "boolean", nullable: false),
                    requiere_firma = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items_plantilla_checklist", x => x.id);
                    table.ForeignKey(
                        name: "FK_items_plantilla_checklist_catalogos_trabajo_tipo_respuesta_~",
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
                name: "documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tipo_documental_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fecha_emision = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    anulado = table.Column<bool>(type: "boolean", nullable: false),
                    anulado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    anulado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    motivo_anulacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    updated_by_user_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    validado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    validado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    rechazado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    rechazado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_vencimiento_validada = table.Column<bool>(type: "boolean", nullable: false),
                    reemplaza_documento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reemplazado_por_documento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    historico = table.Column<bool>(type: "boolean", nullable: false),
                    critico = table.Column<bool>(type: "boolean", nullable: false),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    bloquea_disponibilidad = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_cambio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos", x => x.id);
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
                        name: "FK_documentos_tipos_documentales_tipo_documental_id",
                        column: x => x.tipo_documental_id,
                        principalTable: "tipos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.PrimaryKey("PK_eventos_estado_activo", x => x.id);
                    table.ForeignKey(
                        name: "FK_eventos_estado_activo_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eventos_estado_activo_estados_operacionales_activo_estado_a~",
                        column: x => x.estado_anterior_id,
                        principalTable: "estados_operacionales_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eventos_estado_activo_estados_operacionales_activo_estado_n~",
                        column: x => x.estado_nuevo_id,
                        principalTable: "estados_operacionales_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_trabajo_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_ot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_mantenimiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    aviso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sistema = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    subsistema = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    componente = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    prioridad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criticidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clasificacion_falla_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plan_preventivo_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    preventiva_automatica = table.Column<bool>(type: "boolean", nullable: false),
                    requiere_firma = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_programada_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    inicio_programado_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    fin_programado_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    creado_por_usuario_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    inicio_real_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    finalizacion_tecnico_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    finalizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    cierre_supervisor_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cerrado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    validacion_planificacion_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    validado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    anulado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    anulado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    motivo_anulacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    actualizado_por_usuario_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordenes_trabajo_sql", x => x.id);
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
                name: "versiones_documento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_version = table.Column<int>(type: "integer", nullable: false),
                    codigo_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    archivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_carga_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    cargado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "avisos_trabajo_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aviso_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    estado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sistema = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    subsistema = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    componente = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    prioridad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criticidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitante_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    evidencia_inicial = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    fecha_deteccion_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fecha_creacion_usuario_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    clasificacion_falla_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    evaluado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    aprobado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    aprobado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    rechazado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    rechazado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    anulado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    anulado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    motivo_anulacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    convertido_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    convertido_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "documento_ordenes_trabajo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_documento_ordenes_trabajo", x => x.id);
                    table.ForeignKey(
                        name: "FK_documento_ordenes_trabajo_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documento_ordenes_trabajo_ordenes_trabajo_sql_orden_trabajo~",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ot_estado_historial_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_anterior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_nuevo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "tareas_ot_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_tarea = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    inicio_programado_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    fin_programado_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    requiere_evidencia = table.Column<bool>(type: "boolean", nullable: false),
                    requiere_hh = table.Column<bool>(type: "boolean", nullable: false),
                    checklist_obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "ot_evidencias_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    archivo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_evidencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    es_foto = table.Column<bool>(type: "boolean", nullable: false),
                    es_obligatoria = table.Column<bool>(type: "boolean", nullable: false),
                    cubre_evidencia_obligatoria = table.Column<bool>(type: "boolean", nullable: false),
                    proveedor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    uri_externa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    clave_externa = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ruta_local = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    offline_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    estado_sync = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    creado_por_usuario_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "ot_firmas_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: true),
                    alcance = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    firmante_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    archivo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signature_file_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    firmado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_ot_firmas_sql_tareas_ot_sql_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas_ot_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ot_hh_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tecnico_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    horas = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    fecha_trabajo_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    hora_inicio_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    hora_termino_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    registrado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    validado_supervisor = table.Column<bool>(type: "boolean", nullable: false),
                    validado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    validado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "ot_repuestos_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repuesto_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    unidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bodega_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    estado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_utilizada = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    cantidad_devuelta = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "ot_tecnicos_tarea_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tecnico_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tecnico_nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    asignado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    asignado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    desasignado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    desasignado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    motivo_desasignacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_tecnicos_tarea_sql", x => x.id);
                    table.ForeignKey(
                        name: "FK_ot_tecnicos_tarea_sql_ordenes_trabajo_sql_orden_trabajo_id",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ot_tecnicos_tarea_sql_tareas_ot_sql_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas_ot_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ot_checklists_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_plantilla_id = table.Column<Guid>(type: "uuid", nullable: true),
                    texto_item = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    completado = table.Column<bool>(type: "boolean", nullable: false),
                    completado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    completado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tipo_respuesta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    respuesta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    valor_numerico = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    texto_libre = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    evidencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requiere_foto = table.Column<bool>(type: "boolean", nullable: false),
                    requiere_archivo = table.Column<bool>(type: "boolean", nullable: false),
                    requiere_firma = table.Column<bool>(type: "boolean", nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_ot_checklists_sql_items_plantilla_checklist_item_plantilla_~",
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
                name: "IX_archivos_file_key",
                table: "archivos",
                column: "file_key",
                unique: true);

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
                name: "IX_documento_activos_activo_id",
                table: "documento_activos",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_activos_documento_id_activo_id_vigente",
                table: "documento_activos",
                columns: new[] { "documento_id", "activo_id", "vigente" },
                unique: true,
                filter: "vigente");

            migrationBuilder.CreateIndex(
                name: "IX_documento_faenas_documento_id_faena_id_vigente",
                table: "documento_faenas",
                columns: new[] { "documento_id", "faena_id", "vigente" },
                unique: true,
                filter: "vigente");

            migrationBuilder.CreateIndex(
                name: "IX_documento_faenas_faena_id",
                table: "documento_faenas",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_ordenes_trabajo_documento_id_orden_trabajo_id_vig~",
                table: "documento_ordenes_trabajo",
                columns: new[] { "documento_id", "orden_trabajo_id", "vigente" },
                unique: true,
                filter: "vigente");

            migrationBuilder.CreateIndex(
                name: "IX_documento_ordenes_trabajo_orden_trabajo_id",
                table: "documento_ordenes_trabajo",
                column: "orden_trabajo_id");

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
                name: "IX_documentos_reemplaza_documento_id",
                table: "documentos",
                column: "reemplaza_documento_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_reemplazado_por_documento_id",
                table: "documentos",
                column: "reemplazado_por_documento_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_tipo_documental_id",
                table: "documentos",
                column: "tipo_documental_id");

            migrationBuilder.CreateIndex(
                name: "IX_estados_operacionales_activo_codigo",
                table: "estados_operacionales_activo",
                column: "codigo",
                unique: true);

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
                name: "IX_faenas_codigo",
                table: "faenas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_familias_equipo_codigo",
                table: "familias_equipo",
                column: "codigo",
                unique: true);

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
                name: "IX_ordenes_trabajo_sql_numero_ot",
                table: "ordenes_trabajo_sql",
                column: "numero_ot",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_prioridad_id",
                table: "ordenes_trabajo_sql",
                column: "prioridad_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_tipo_mantenimiento_id",
                table: "ordenes_trabajo_sql",
                column: "tipo_mantenimiento_id");

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
                name: "IX_ot_evidencias_sql_archivo_id",
                table: "ot_evidencias_sql",
                column: "archivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_orden_trabajo_id",
                table: "ot_evidencias_sql",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_tarea_id",
                table: "ot_evidencias_sql",
                column: "tarea_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_evidencias_sql_tipo_evidencia_id",
                table: "ot_evidencias_sql",
                column: "tipo_evidencia_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_archivo_id",
                table: "ot_firmas_sql",
                column: "archivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_orden_trabajo_id",
                table: "ot_firmas_sql",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_firmas_sql_tarea_id",
                table: "ot_firmas_sql",
                column: "tarea_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_orden_trabajo_id",
                table: "ot_hh_sql",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_hh_sql_tarea_id",
                table: "ot_hh_sql",
                column: "tarea_id");

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
                name: "IX_ot_tecnicos_tarea_sql_orden_trabajo_id",
                table: "ot_tecnicos_tarea_sql",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_ot_tecnicos_tarea_sql_tarea_id_tecnico_usuario_id_vigente",
                table: "ot_tecnicos_tarea_sql",
                columns: new[] { "tarea_id", "tecnico_usuario_id", "vigente" },
                unique: true,
                filter: "vigente");

            migrationBuilder.CreateIndex(
                name: "IX_permisos_codigo",
                table: "permisos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_checklist_codigo",
                table: "plantillas_checklist",
                column: "codigo",
                unique: true);

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
                name: "IX_tareas_ot_sql_orden_trabajo_id_codigo_tarea",
                table: "tareas_ot_sql",
                columns: new[] { "orden_trabajo_id", "codigo_tarea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipos_documentales_codigo",
                table: "tipos_documentales",
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
                name: "IX_ubicaciones_bodega_bodega_id_codigo",
                table: "ubicaciones_bodega",
                columns: new[] { "bodega_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_faenas_faena_id",
                table: "usuario_faenas",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_faenas_usuario_id_faena_id_vigente",
                table: "usuario_faenas",
                columns: new[] { "usuario_id", "faena_id", "vigente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_roles_rol_id",
                table: "usuario_roles",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_roles_usuario_id_rol_id_vigente",
                table: "usuario_roles",
                columns: new[] { "usuario_id", "rol_id", "vigente" },
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
                filter: "vigente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "avisos_trabajo_sql");

            migrationBuilder.DropTable(
                name: "documento_activos");

            migrationBuilder.DropTable(
                name: "documento_faenas");

            migrationBuilder.DropTable(
                name: "documento_ordenes_trabajo");

            migrationBuilder.DropTable(
                name: "eventos_estado_activo");

            migrationBuilder.DropTable(
                name: "movimientos_stock");

            migrationBuilder.DropTable(
                name: "ot_checklists_sql");

            migrationBuilder.DropTable(
                name: "ot_estado_historial_sql");

            migrationBuilder.DropTable(
                name: "ot_hh_sql");

            migrationBuilder.DropTable(
                name: "ot_repuestos_sql");

            migrationBuilder.DropTable(
                name: "ot_tecnicos_tarea_sql");

            migrationBuilder.DropTable(
                name: "rol_permisos");

            migrationBuilder.DropTable(
                name: "stock_bodega");

            migrationBuilder.DropTable(
                name: "usuario_faenas");

            migrationBuilder.DropTable(
                name: "usuario_roles");

            migrationBuilder.DropTable(
                name: "versiones_documento");

            migrationBuilder.DropTable(
                name: "reservas_stock");

            migrationBuilder.DropTable(
                name: "transferencias_stock");

            migrationBuilder.DropTable(
                name: "items_plantilla_checklist");

            migrationBuilder.DropTable(
                name: "ot_evidencias_sql");

            migrationBuilder.DropTable(
                name: "ot_firmas_sql");

            migrationBuilder.DropTable(
                name: "permisos");

            migrationBuilder.DropTable(
                name: "ubicaciones_bodega");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "documentos");

            migrationBuilder.DropTable(
                name: "repuestos");

            migrationBuilder.DropTable(
                name: "plantillas_checklist");

            migrationBuilder.DropTable(
                name: "archivos");

            migrationBuilder.DropTable(
                name: "tareas_ot_sql");

            migrationBuilder.DropTable(
                name: "bodegas");

            migrationBuilder.DropTable(
                name: "tipos_documentales");

            migrationBuilder.DropTable(
                name: "ordenes_trabajo_sql");

            migrationBuilder.DropTable(
                name: "catalogos_inventario");

            migrationBuilder.DropTable(
                name: "activos");

            migrationBuilder.DropTable(
                name: "catalogos_trabajo");

            migrationBuilder.DropTable(
                name: "estados_operacionales_activo");

            migrationBuilder.DropTable(
                name: "faenas");

            migrationBuilder.DropTable(
                name: "familias_equipo");
        }
    }
}
