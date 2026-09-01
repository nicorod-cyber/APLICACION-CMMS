using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RelationalOperationalModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public.conjuntos_datos_operacionales') IS NOT NULL
                       AND EXISTS (SELECT 1 FROM conjuntos_datos_operacionales) THEN
                        RAISE EXCEPTION 'Migration refused: conjuntos_datos_operacionales contains data. Back it up, classify it, and run the controlled development cleanup before applying this migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "conjuntos_datos_operacionales");

            migrationBuilder.CreateTable(
                name: "contratos_disponibilidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    cliente = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: false),
                    horas_comprometidas_dia = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    disponibilidad_objetivo = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    fecha_inicio_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    fecha_fin_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reglas_cliente = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "importaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    esquema = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    archivo_original = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    archivo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    solo_simulacion = table.Column<bool>(type: "boolean", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    cargado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cargado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    aplicado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    aplicado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    rechazado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    rechazado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "planes_preventivos_sql",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    tipo_frecuencia = table.Column<int>(type: "integer", nullable: false),
                    frecuencia_horas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    frecuencia_km = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    frecuencia_dias = table.Column<int>(type: "integer", nullable: true),
                    tolerancia_horas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tolerancia_km = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tolerancia_dias = table.Column<int>(type: "integer", nullable: false),
                    plantilla_checklist_id = table.Column<Guid>(type: "uuid", nullable: true),
                    repuestos_sugeridos = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    hh_estimadas = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    fecha_inicio_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ultima_ejecucion_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ultima_ejecucion_horas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ultima_ejecucion_km = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    proxima_fecha_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    proxima_hora = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    proximo_km = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planes_preventivos_sql", x => x.id);
                    table.CheckConstraint("ck_planes_preventivos_frecuencias", "frecuencia_horas IS NOT NULL OR frecuencia_km IS NOT NULL OR frecuencia_dias IS NOT NULL");
                    table.CheckConstraint("ck_planes_preventivos_hh", "hh_estimadas > 0");
                    table.ForeignKey(
                        name: "FK_planes_preventivos_sql_plantillas_checklist_plantilla_check~",
                        column: x => x.plantilla_checklist_id,
                        principalTable: "plantillas_checklist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proveedores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rut = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    contacto = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    email = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    telefono = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    direccion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    lead_time_esperado_dias = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proveedores", x => x.id);
                    table.CheckConstraint("ck_proveedores_lead_time", "lead_time_esperado_dias >= 0");
                });

            migrationBuilder.CreateTable(
                name: "solicitudes_abastecimiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_solicitud = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    solicitud_material_id = table.Column<Guid>(type: "uuid", nullable: true),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    solicitada_tecnica_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    aprobada_mantenimiento_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    enviada_abastecimiento_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_solicitudes_abastecimiento_ordenes_trabajo_sql_orden_trabaj~",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitudes_abastecimiento_solicitudes_repuestos_solicitud_~",
                        column: x => x.solicitud_material_id,
                        principalTable: "solicitudes_repuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "talleres",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capacidad_diaria_hh = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    capacidad_equipos = table.Column<int>(type: "integer", nullable: false),
                    horario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    especialidad = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_talleres", x => x.id);
                    table.CheckConstraint("ck_talleres_capacidad_equipos", "capacidad_equipos >= 0");
                    table.CheckConstraint("ck_talleres_capacidad_hh", "capacidad_diaria_hh >= 0");
                    table.ForeignKey(
                        name: "FK_talleres_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contrato_disponibilidad_objetivos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rol = table.Column<int>(type: "integer", nullable: false),
                    fecha_inicio_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    fecha_fin_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_contrato_disponibilidad_objetivos_contratos_disponibilidad_~",
                        column: x => x.contrato_id,
                        principalTable: "contratos_disponibilidad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contrato_disponibilidad_objetivos_unidades_operativas_unida~",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "errores_importacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    importacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_fila = table.Column<int>(type: "integer", nullable: false),
                    columna = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    mensaje = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    importacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    detalle = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    importacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_fila = table.Column<int>(type: "integer", nullable: false),
                    operacion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    snapshot_entrada = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "alcances_plan_preventivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_preventivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    familia_equipo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_activo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    marca = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    modelo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_alcances_plan_preventivo_planes_preventivos_sql_plan_preven~",
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
                        name: "FK_alcances_plan_preventivo_unidades_operativas_unidad_operati~",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluaciones_preventivas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_preventivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    evaluado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    horas_actuales = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    km_actuales = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    evaluado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_evaluaciones_preventivas_ordenes_trabajo_sql_orden_trabajo_~",
                        column: x => x.orden_trabajo_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluaciones_preventivas_planes_preventivos_sql_plan_preven~",
                        column: x => x.plan_preventivo_id,
                        principalTable: "planes_preventivos_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "historial_preventivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_preventivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_anterior = table.Column<int>(type: "integer", nullable: false),
                    estado_nuevo = table.Column<int>(type: "integer", nullable: false),
                    fecha_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_historial_preventivo_planes_preventivos_sql_plan_preventivo~",
                        column: x => x.plan_preventivo_id,
                        principalTable: "planes_preventivos_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "detalle_solicitud_abastecimiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitud_abastecimiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repuesto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_solicitud_externa = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    cantidad_solicitada = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cantidad_recibida = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cantidad_entregada = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    costo_estimado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    moneda = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    documento_respaldo_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_detalle_solicitud_abastecimiento_solicitudes_abastecimiento~",
                        column: x => x.solicitud_abastecimiento_id,
                        principalTable: "solicitudes_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_compra",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_oc = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    solicitud_abastecimiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_oc_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fecha_comprometida_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    costo_oc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    moneda = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    documento_oc_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_ordenes_compra_solicitudes_abastecimiento_solicitud_abastec~",
                        column: x => x.solicitud_abastecimiento_id,
                        principalTable: "solicitudes_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alertas_programacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    severidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    mensaje = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    taller_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resuelta = table.Column<bool>(type: "boolean", nullable: false),
                    creada_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "programaciones_ot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    taller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fin_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    hh_estimadas = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    tecnico_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "eventos_disponibilidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asignacion_contrato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    causa = table.Column<int>(type: "integer", nullable: false),
                    inicio_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fin_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    puede_utilizarse = table.Column<bool>(type: "boolean", nullable: false),
                    atribuible_mantenimiento = table.Column<bool>(type: "boolean", nullable: false),
                    comentario = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_eventos_disponibilidad_contrato_disponibilidad_objetivos_as~",
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
                        name: "FK_eventos_disponibilidad_unidades_operativas_unidad_operativa~",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "detalle_orden_compra",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    detalle_solicitud_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_orden_compra", x => x.id);
                    table.CheckConstraint("ck_detalle_orden_compra_cantidad", "cantidad > 0");
                    table.ForeignKey(
                        name: "FK_detalle_orden_compra_detalle_solicitud_abastecimiento_detal~",
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitud_abastecimiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_compra_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_recepcion_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    despacho_directo_ot = table.Column<bool>(type: "boolean", nullable: false),
                    movimiento_recepcion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    movimiento_entrega_id = table.Column<Guid>(type: "uuid", nullable: true),
                    costo_real = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    documento_recepcion_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    documento_entrega_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_recepciones_abastecimiento_movimientos_stock_movimiento_ent~",
                        column: x => x.movimiento_entrega_id,
                        principalTable: "movimientos_stock",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recepciones_abastecimiento_movimientos_stock_movimiento_rec~",
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
                        name: "FK_recepciones_abastecimiento_solicitudes_abastecimiento_solic~",
                        column: x => x.solicitud_abastecimiento_id,
                        principalTable: "solicitudes_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dependencias_programacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    programacion_predecesora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    programacion_sucesora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dependencias_programacion", x => x.id);
                    table.CheckConstraint("ck_dependencias_programacion_distintas", "programacion_predecesora_id <> programacion_sucesora_id");
                    table.ForeignKey(
                        name: "FK_dependencias_programacion_programaciones_ot_programacion_pr~",
                        column: x => x.programacion_predecesora_id,
                        principalTable: "programaciones_ot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dependencias_programacion_programaciones_ot_programacion_su~",
                        column: x => x.programacion_sucesora_id,
                        principalTable: "programaciones_ot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "detalle_recepcion_abastecimiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recepcion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    detalle_solicitud_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_recibida = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cantidad_entregada = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_recepcion_abastecimiento", x => x.id);
                    table.CheckConstraint("ck_detalle_recepcion_abastecimiento_cantidades", "cantidad_recibida >= 0 AND cantidad_entregada >= 0 AND cantidad_entregada <= cantidad_recibida");
                    table.ForeignKey(
                        name: "FK_detalle_recepcion_abastecimiento_detalle_solicitud_abasteci~",
                        column: x => x.detalle_solicitud_id,
                        principalTable: "detalle_solicitud_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalle_recepcion_abastecimiento_recepciones_abastecimiento~",
                        column: x => x.recepcion_id,
                        principalTable: "recepciones_abastecimiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "IX_contrato_disponibilidad_objetivos_activo_id",
                table: "contrato_disponibilidad_objetivos",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_disponibilidad_objetivos_contrato_id_activo_id_act~",
                table: "contrato_disponibilidad_objetivos",
                columns: new[] { "contrato_id", "activo_id", "activo" },
                unique: true,
                filter: "activo_id IS NOT NULL AND activo");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_disponibilidad_objetivos_contrato_id_unidad_operat~",
                table: "contrato_disponibilidad_objetivos",
                columns: new[] { "contrato_id", "unidad_operativa_id", "activo" },
                unique: true,
                filter: "unidad_operativa_id IS NOT NULL AND activo");

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
                name: "IX_dependencias_programacion_programacion_predecesora_id_progr~",
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
                name: "IX_detalle_recepcion_abastecimiento_recepcion_id_detalle_solic~",
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
                name: "IX_errores_importacion_importacion_id",
                table: "errores_importacion",
                column: "importacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluaciones_preventivas_activo_id",
                table: "evaluaciones_preventivas",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluaciones_preventivas_orden_trabajo_id",
                table: "evaluaciones_preventivas",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluaciones_preventivas_plan_preventivo_id_activo_id_evalu~",
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
                name: "IX_eventos_importacion_importacion_id_fecha_utc",
                table: "eventos_importacion",
                columns: new[] { "importacion_id", "fecha_utc" });

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
                name: "IX_recepciones_abastecimiento_solicitud_abastecimiento_id_fech~",
                table: "recepciones_abastecimiento",
                columns: new[] { "solicitud_abastecimiento_id", "fecha_recepcion_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_abastecimiento_activo_id",
                table: "solicitudes_abastecimiento",
                column: "activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_abastecimiento_bodega_id",
                table: "solicitudes_abastecimiento",
                column: "bodega_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_abastecimiento_estado_enviada_abastecimiento_at~",
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
                name: "IX_talleres_codigo",
                table: "talleres",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_talleres_faena_id",
                table: "talleres",
                column: "faena_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alcances_plan_preventivo");

            migrationBuilder.DropTable(
                name: "alertas_programacion");

            migrationBuilder.DropTable(
                name: "dependencias_programacion");

            migrationBuilder.DropTable(
                name: "detalle_orden_compra");

            migrationBuilder.DropTable(
                name: "detalle_recepcion_abastecimiento");

            migrationBuilder.DropTable(
                name: "errores_importacion");

            migrationBuilder.DropTable(
                name: "evaluaciones_preventivas");

            migrationBuilder.DropTable(
                name: "eventos_disponibilidad");

            migrationBuilder.DropTable(
                name: "eventos_importacion");

            migrationBuilder.DropTable(
                name: "filas_importacion");

            migrationBuilder.DropTable(
                name: "historial_preventivo");

            migrationBuilder.DropTable(
                name: "programaciones_ot");

            migrationBuilder.DropTable(
                name: "detalle_solicitud_abastecimiento");

            migrationBuilder.DropTable(
                name: "recepciones_abastecimiento");

            migrationBuilder.DropTable(
                name: "contrato_disponibilidad_objetivos");

            migrationBuilder.DropTable(
                name: "importaciones");

            migrationBuilder.DropTable(
                name: "planes_preventivos_sql");

            migrationBuilder.DropTable(
                name: "talleres");

            migrationBuilder.DropTable(
                name: "ordenes_compra");

            migrationBuilder.DropTable(
                name: "contratos_disponibilidad");

            migrationBuilder.DropTable(
                name: "proveedores");

            migrationBuilder.DropTable(
                name: "solicitudes_abastecimiento");

            migrationBuilder.CreateTable(
                name: "conjuntos_datos_operacionales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    contenido = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conjuntos_datos_operacionales", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conjuntos_datos_operacionales_codigo",
                table: "conjuntos_datos_operacionales",
                column: "codigo",
                unique: true);
        }
    }
}

