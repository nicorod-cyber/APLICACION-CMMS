using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AssetTransferUnitDocumentCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_unidades_operativas_estados_operacionales_activo_estado_ope~",
                table: "unidades_operativas");

            migrationBuilder.DropIndex(
                name: "IX_componentes_unidad_operativa_activo_id_fecha_desmontaje_utc",
                table: "componentes_unidad_operativa");

            migrationBuilder.DropIndex(
                name: "IX_componentes_unidad_operativa_unidad_operativa_id",
                table: "componentes_unidad_operativa");

            migrationBuilder.AddColumn<Guid>(
                name: "ciclo_correccion_id",
                table: "versiones_documento",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado_correccion",
                table: "versiones_documento",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado_validacion",
                table: "versiones_documento",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_emision",
                table: "versiones_documento",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_vencimiento",
                table: "versiones_documento",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_rechazo",
                table: "versiones_documento",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacion_correccion",
                table: "versiones_documento",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rechazado_por_usuario_id",
                table: "versiones_documento",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rechazado_utc",
                table: "versiones_documento",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reemplaza_version_id",
                table: "versiones_documento",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "responsable_correccion_usuario_id",
                table: "versiones_documento",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "validado_por_usuario_id",
                table: "versiones_documento",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "validado_utc",
                table: "versiones_documento",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "estado_derivado_calculado_utc",
                table: "unidades_operativas",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "estado_derivado_por_activo_id",
                table: "unidades_operativas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "estado_operacional_base_id",
                table: "unidades_operativas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_estado_derivado",
                table: "unidades_operativas",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "critico",
                table: "roles_componente_unidad",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "matriz_documental_version_id",
                table: "ordenes_trabajo_sql",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "antecedente_id",
                table: "eventos_estado_activo",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_antecedente",
                table: "eventos_estado_activo",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "severidad",
                table: "estados_operacionales_activo",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "desmontado_por_usuario_id",
                table: "componentes_unidad_operativa",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "montado_por_usuario_id",
                table: "componentes_unidad_operativa",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "motivo_desmontaje",
                table: "componentes_unidad_operativa",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_montaje",
                table: "componentes_unidad_operativa",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rol_critico_codigo",
                table: "componentes_unidad_operativa",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alias_identificador_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_identificador = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ambito = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    valor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    valor_normalizado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    vigencia_desde_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    vigencia_hasta_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reemplazado_por_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_alias_identificador_activo_alias_identificador_activo_reemp~",
                        column: x => x.reemplazado_por_alias_id,
                        principalTable: "alias_identificador_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "matrices_requisitos_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    numero_version = table.Column<int>(type: "integer", nullable: false),
                    vigencia_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_equipo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    motivo_cambio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matrices_requisitos_documentales", x => x.id);
                    table.CheckConstraint("ck_matrices_requisitos_estado", "estado IN ('BORRADOR','VIGENTE','REEMPLAZADA','ANULADA')");
                    table.CheckConstraint("ck_matrices_requisitos_version", "numero_version > 0");
                    table.CheckConstraint("ck_matrices_requisitos_vigencias", "vigencia_hasta IS NULL OR vigencia_hasta >= vigencia_desde");
                    table.ForeignKey(
                        name: "FK_matrices_requisitos_documentales_familias_equipo_familia_eq~",
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
                name: "traslados_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    faena_origen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    faena_destino_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidad_operativa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_efectiva_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    fecha_registro_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_traslados_activo", x => x.id);
                    table.CheckConstraint("ck_traslados_activo_origen_destino", "faena_origen_id IS DISTINCT FROM faena_destino_id");
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
                name: "detalles_matriz_requisitos_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    matriz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documental_id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    critico = table.Column<bool>(type: "boolean", nullable: false),
                    bloquea_disponibilidad = table.Column<bool>(type: "boolean", nullable: false),
                    requiere_fecha_vencimiento = table.Column<bool>(type: "boolean", nullable: false),
                    dias_anticipacion = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalles_matriz_requisitos_documentales", x => x.id);
                    table.CheckConstraint("ck_detalles_matriz_dias_anticipacion", "dias_anticipacion >= 0");
                    table.ForeignKey(
                        name: "FK_detalles_matriz_requisitos_documentales_matrices_requisitos~",
                        column: x => x.matriz_id,
                        principalTable: "matrices_requisitos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_detalles_matriz_requisitos_documentales_tipos_documentales_~",
                        column: x => x.tipo_documental_id,
                        principalTable: "tipos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vigencias_ubicacion_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vigencia_desde_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    vigencia_hasta_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    traslado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "detalles_ot_documental",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matriz_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matriz_detalle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_origen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_documento_origen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clave_ciclo = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    aplicable = table.Column<bool>(type: "boolean", nullable: false),
                    observacion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    completado_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_detalles_ot_documental_detalles_matriz_requisitos_documenta~",
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
                        name: "FK_detalles_ot_documental_matrices_requisitos_documentales_mat~",
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
                        name: "FK_detalles_ot_documental_versiones_documento_version_document~",
                        column: x => x.version_documento_origen_id,
                        principalTable: "versiones_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS btree_gist;

                UPDATE estados_operacionales_activo SET severidad = CASE UPPER(codigo)
                    WHEN 'OPERATIVO_FAENA' THEN 0 WHEN 'ALERTA_FAENA' THEN 25
                    WHEN 'FUERA_SERVICIO_FAENA' THEN 100 WHEN 'FUERA_SERVICIO_TALLER' THEN 100
                    WHEN 'DADO_DE_BAJA' THEN 200 ELSE 50 END;

                UPDATE roles_componente_unidad SET critico = TRUE WHERE UPPER(codigo) IN ('FABRICA','CHASIS');
                UPDATE componentes_unidad_operativa c
                   SET montado_por_usuario_id = COALESCE(NULLIF(c.montado_por_usuario_id,''), 'migration'),
                       rol_critico_codigo = CASE WHEN r.critico THEN UPPER(r.codigo) ELSE NULL END
                  FROM roles_componente_unidad r WHERE r.id = c.rol_componente_id;

                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY activo_id ORDER BY fecha_montaje_utc DESC, created_at_utc DESC, id DESC) rn
                    FROM componentes_unidad_operativa WHERE fecha_desmontaje_utc IS NULL)
                UPDATE componentes_unidad_operativa c
                   SET fecha_desmontaje_utc = GREATEST(c.fecha_montaje_utc + interval '1 microsecond', transaction_timestamp()),
                       desmontado_por_usuario_id = 'migration', motivo_desmontaje = 'Regularizacion de duplicidad vigente previa a constraint'
                  FROM ranked r WHERE c.id = r.id AND r.rn > 1;

                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY unidad_operativa_id, rol_critico_codigo ORDER BY fecha_montaje_utc DESC, created_at_utc DESC, id DESC) rn
                    FROM componentes_unidad_operativa WHERE fecha_desmontaje_utc IS NULL AND rol_critico_codigo IS NOT NULL)
                UPDATE componentes_unidad_operativa c
                   SET fecha_desmontaje_utc = GREATEST(c.fecha_montaje_utc + interval '1 microsecond', transaction_timestamp()),
                       desmontado_por_usuario_id = 'migration', motivo_desmontaje = 'Regularizacion de rol critico duplicado previo a constraint'
                  FROM ranked r WHERE c.id = r.id AND r.rn > 1;

                UPDATE unidades_operativas SET estado_operacional_base_id = estado_operacional_id WHERE estado_operacional_base_id IS NULL;
                WITH restrictive AS (
                    SELECT c.unidad_operativa_id, c.activo_id, a.estado_operacional_id,
                           ROW_NUMBER() OVER (PARTITION BY c.unidad_operativa_id ORDER BY e.severidad DESC, r.codigo, a.codigo) rn
                    FROM componentes_unidad_operativa c JOIN activos a ON a.id=c.activo_id
                    JOIN estados_operacionales_activo e ON e.id=a.estado_operacional_id
                    JOIN roles_componente_unidad r ON r.id=c.rol_componente_id
                    WHERE c.fecha_desmontaje_utc IS NULL)
                UPDATE unidades_operativas u SET estado_operacional_id=r.estado_operacional_id, estado_derivado_por_activo_id=r.activo_id,
                    motivo_estado_derivado='Estado inicial derivado durante migracion', estado_derivado_calculado_utc=transaction_timestamp()
                FROM restrictive r WHERE r.rn=1 AND r.unidad_operativa_id=u.id;

                UPDATE versiones_documento v SET fecha_emision=d.fecha_emision, fecha_vencimiento=d.fecha_vencimiento,
                    estado_validacion=CASE WHEN d.estado ILIKE '%rechaz%' THEN 'Rechazado' WHEN d.estado ILIKE '%valid%' OR d.estado ILIKE '%vigente%' THEN 'Vigente' ELSE 'PendienteValidacion' END,
                    validado_por_usuario_id=d.validado_por_usuario_id, validado_utc=d.validado_at_utc,
                    rechazado_por_usuario_id=d.rechazado_por_usuario_id, rechazado_utc=d.rechazado_at_utc, motivo_rechazo=d.motivo_rechazo,
                    responsable_correccion_usuario_id=CASE WHEN d.estado ILIKE '%rechaz%' THEN v.cargado_por_usuario_id ELSE NULL END,
                    estado_correccion=CASE WHEN d.estado ILIKE '%rechaz%' THEN 'OBSERVADO' ELSE 'CERRADO_MIGRADO' END,
                    observacion_correccion=CASE WHEN d.estado ILIKE '%rechaz%' THEN d.motivo_rechazo ELSE NULL END
                FROM documentos d WHERE d.id=v.documento_id;

                INSERT INTO vigencias_ubicacion_activo(id,activo_id,faena_id,vigencia_desde_utc,vigencia_hasta_utc,traslado_id,created_at_utc)
                SELECT gen_random_uuid(),a.id,a.faena_id,a.created_at_utc,NULL,NULL,transaction_timestamp() FROM activos a
                WHERE NOT EXISTS (SELECT 1 FROM vigencias_ubicacion_activo v WHERE v.activo_id=a.id);

                WITH ranked AS (
                    SELECT id, numero_serie, COALESCE(familia_equipo_id,tipo_activo_id) scope_id, created_at_utc,
                           ROW_NUMBER() OVER (PARTITION BY COALESCE(familia_equipo_id,tipo_activo_id), UPPER(TRIM(numero_serie)) ORDER BY created_at_utc,id) rn
                    FROM activos WHERE NULLIF(TRIM(numero_serie),'') IS NOT NULL)
                INSERT INTO alias_identificador_activo(id,activo_id,tipo_identificador,ambito,valor,valor_normalizado,vigencia_desde_utc,vigencia_hasta_utc,created_at_utc)
                SELECT gen_random_uuid(),id,'NUMERO_SERIE','SERIAL:'||replace(scope_id::text,'-',''),numero_serie,UPPER(TRIM(numero_serie)),created_at_utc,transaction_timestamp(),transaction_timestamp()
                FROM ranked WHERE rn>1;
                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY COALESCE(familia_equipo_id,tipo_activo_id), UPPER(TRIM(numero_serie)) ORDER BY created_at_utc,id) rn
                    FROM activos WHERE NULLIF(TRIM(numero_serie),'') IS NOT NULL)
                UPDATE activos a SET numero_serie=a.numero_serie||'-DUP-'||left(replace(a.id::text,'-',''),8), updated_at_utc=transaction_timestamp()
                FROM ranked r WHERE a.id=r.id AND r.rn>1;
                INSERT INTO alias_identificador_activo(id,activo_id,tipo_identificador,ambito,valor,valor_normalizado,vigencia_desde_utc,vigencia_hasta_utc,created_at_utc)
                SELECT gen_random_uuid(),a.id,'NUMERO_SERIE','SERIAL:'||replace(COALESCE(a.familia_equipo_id,a.tipo_activo_id)::text,'-',''),a.numero_serie,UPPER(TRIM(a.numero_serie)),transaction_timestamp(),NULL,transaction_timestamp()
                FROM activos a WHERE NULLIF(TRIM(a.numero_serie),'') IS NOT NULL;

                INSERT INTO matrices_requisitos_documentales(id,codigo,numero_version,vigencia_desde,vigencia_hasta,estado,tipo_activo_id,familia_equipo_id,creado_por_usuario_id,motivo_cambio,created_at_utc)
                SELECT gen_random_uuid(),'LEGACY-'||left(replace(tipo_activo_id::text,'-',''),12)||'-'||COALESCE(left(replace(familia_equipo_id::text,'-',''),12),'GENERAL'),1,CURRENT_DATE,NULL,'VIGENTE',tipo_activo_id,familia_equipo_id,'migration','Migracion inicial desde requisitos documentales existentes',transaction_timestamp()
                FROM requisitos_documentales_tipo_activo WHERE activo GROUP BY tipo_activo_id,familia_equipo_id;
                INSERT INTO detalles_matriz_requisitos_documentales(id,matriz_id,tipo_documental_id,obligatorio,critico,bloquea_disponibilidad,requiere_fecha_vencimiento,dias_anticipacion,created_at_utc)
                SELECT gen_random_uuid(),m.id,r.tipo_documental_id,r.obligatorio,r.critico,r.bloquea_disponibilidad,r.requiere_fecha_vencimiento,COALESCE(r.dias_alerta,45),transaction_timestamp()
                FROM requisitos_documentales_tipo_activo r JOIN matrices_requisitos_documentales m ON m.tipo_activo_id=r.tipo_activo_id AND m.familia_equipo_id IS NOT DISTINCT FROM r.familia_equipo_id AND m.numero_version=1 AND m.codigo LIKE 'LEGACY-%'
                WHERE r.activo;

                CREATE UNIQUE INDEX ux_activos_numero_serie_ambito ON activos ((COALESCE(familia_equipo_id,tipo_activo_id)), UPPER(TRIM(numero_serie))) WHERE NULLIF(TRIM(numero_serie),'') IS NOT NULL;
                ALTER TABLE vigencias_ubicacion_activo ADD CONSTRAINT ex_vigencias_ubicacion_activo_sin_solape EXCLUDE USING gist (activo_id WITH =, tstzrange(vigencia_desde_utc,COALESCE(vigencia_hasta_utc,'infinity'::timestamptz),'[)') WITH &&);
                ALTER TABLE matrices_requisitos_documentales ADD CONSTRAINT ex_matrices_requisitos_sin_solape EXCLUDE USING gist (tipo_activo_id WITH =, COALESCE(familia_equipo_id,'00000000-0000-0000-0000-000000000000'::uuid) WITH =, daterange(vigencia_desde,COALESCE(vigencia_hasta,'infinity'::date),'[]') WITH &&) WHERE (estado='VIGENTE');

                CREATE OR REPLACE FUNCTION cmms_set_critical_role() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE role_code text; role_critical boolean; BEGIN SELECT UPPER(codigo),critico INTO role_code,role_critical FROM roles_componente_unidad WHERE id=NEW.rol_componente_id; NEW.rol_critico_codigo:=CASE WHEN role_critical THEN role_code ELSE NULL END; RETURN NEW; END $$;
                CREATE TRIGGER trg_component_critical_role BEFORE INSERT OR UPDATE OF rol_componente_id ON componentes_unidad_operativa FOR EACH ROW EXECUTE FUNCTION cmms_set_critical_role();

                CREATE OR REPLACE FUNCTION cmms_enforce_unique_asset_attribute() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE unique_required boolean; lock_key text; BEGIN SELECT es_unico INTO unique_required FROM definiciones_atributo_activo WHERE id=NEW.definicion_atributo_id;
                IF unique_required THEN lock_key:=NEW.definicion_atributo_id::text||'|'||COALESCE(UPPER(TRIM(NEW.valor_texto)),NEW.valor_numerico::text,NEW.valor_booleano::text,NEW.valor_fecha::text,''); PERFORM pg_advisory_xact_lock(hashtextextended(lock_key,0));
                IF EXISTS(SELECT 1 FROM valores_atributo_activo v WHERE v.definicion_atributo_id=NEW.definicion_atributo_id AND v.activo_id<>NEW.activo_id AND v.valor_texto IS NOT DISTINCT FROM NEW.valor_texto AND v.valor_numerico IS NOT DISTINCT FROM NEW.valor_numerico AND v.valor_booleano IS NOT DISTINCT FROM NEW.valor_booleano AND v.valor_fecha IS NOT DISTINCT FROM NEW.valor_fecha) THEN RAISE EXCEPTION 'Identificador dinamico duplicado' USING ERRCODE='unique_violation'; END IF; END IF; RETURN NEW; END $$;
                CREATE TRIGGER trg_unique_asset_attribute BEFORE INSERT OR UPDATE ON valores_atributo_activo FOR EACH ROW EXECUTE FUNCTION cmms_enforce_unique_asset_attribute();

                CREATE OR REPLACE FUNCTION cmms_require_asset_state_event() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NOT EXISTS(SELECT 1 FROM eventos_estado_activo e WHERE e.activo_id=NEW.id AND e.estado_anterior_id=OLD.estado_operacional_id AND e.estado_nuevo_id=NEW.estado_operacional_id AND e.id::text=current_setting('cmms.asset_state_event_id',true)) THEN RAISE EXCEPTION 'El estado operacional solo puede cambiar mediante un evento de estado' USING ERRCODE='check_violation'; END IF; RETURN NULL; END $$;
                CREATE CONSTRAINT TRIGGER trg_asset_state_event AFTER UPDATE OF estado_operacional_id ON activos DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION cmms_require_asset_state_event();
                CREATE OR REPLACE FUNCTION cmms_require_asset_transfer() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NOT EXISTS(SELECT 1 FROM traslados_activo t WHERE t.activo_id=NEW.id AND t.faena_origen_id IS NOT DISTINCT FROM OLD.faena_id AND t.faena_destino_id IS NOT DISTINCT FROM NEW.faena_id AND t.id::text=ANY(string_to_array(current_setting('cmms.asset_transfer_ids',true),','))) THEN RAISE EXCEPTION 'La faena solo puede cambiar mediante un traslado' USING ERRCODE='check_violation'; END IF; RETURN NULL; END $$;
                CREATE CONSTRAINT TRIGGER trg_asset_transfer AFTER UPDATE OF faena_id ON activos DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION cmms_require_asset_transfer();

                CREATE OR REPLACE FUNCTION cmms_prevent_critical_delete() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'No se permiten borrados fisicos en historial critico CMMS' USING ERRCODE='check_violation'; END $$;
                CREATE TRIGGER trg_no_delete_assets BEFORE DELETE ON activos FOR EACH ROW EXECUTE FUNCTION cmms_prevent_critical_delete();
                CREATE TRIGGER trg_no_delete_asset_state_events BEFORE DELETE ON eventos_estado_activo FOR EACH ROW EXECUTE FUNCTION cmms_prevent_critical_delete();
                CREATE TRIGGER trg_no_delete_asset_transfers BEFORE DELETE ON traslados_activo FOR EACH ROW EXECUTE FUNCTION cmms_prevent_critical_delete();
                CREATE TRIGGER trg_no_delete_asset_locations BEFORE DELETE ON vigencias_ubicacion_activo FOR EACH ROW EXECUTE FUNCTION cmms_prevent_critical_delete();
                CREATE TRIGGER trg_no_delete_documents BEFORE DELETE ON documentos FOR EACH ROW EXECUTE FUNCTION cmms_prevent_critical_delete();
                CREATE TRIGGER trg_no_delete_document_versions BEFORE DELETE ON versiones_documento FOR EACH ROW EXECUTE FUNCTION cmms_prevent_critical_delete();
                CREATE TRIGGER trg_no_delete_unit_components BEFORE DELETE ON componentes_unidad_operativa FOR EACH ROW EXECUTE FUNCTION cmms_prevent_critical_delete();
                CREATE TRIGGER trg_no_delete_work_orders BEFORE DELETE ON ordenes_trabajo_sql FOR EACH ROW EXECUTE FUNCTION cmms_prevent_critical_delete();
                CREATE TRIGGER trg_no_delete_document_matrices BEFORE DELETE ON matrices_requisitos_documentales FOR EACH ROW EXECUTE FUNCTION cmms_prevent_critical_delete();
                CREATE TRIGGER trg_no_delete_documentary_details BEFORE DELETE ON detalles_ot_documental FOR EACH ROW EXECUTE FUNCTION cmms_prevent_critical_delete();
                """);

            migrationBuilder.CreateIndex(
                name: "IX_versiones_documento_reemplaza_version_id",
                table: "versiones_documento",
                column: "reemplaza_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidades_operativas_estado_derivado_por_activo_id",
                table: "unidades_operativas",
                column: "estado_derivado_por_activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidades_operativas_estado_operacional_base_id",
                table: "unidades_operativas",
                column: "estado_operacional_base_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_trabajo_sql_matriz_documental_version_id",
                table: "ordenes_trabajo_sql",
                column: "matriz_documental_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_componentes_unidad_operativa_activo_id",
                table: "componentes_unidad_operativa",
                column: "activo_id",
                unique: true,
                filter: "fecha_desmontaje_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_componentes_unidad_operativa_unidad_operativa_id_rol_critic~",
                table: "componentes_unidad_operativa",
                columns: new[] { "unidad_operativa_id", "rol_critico_codigo" },
                unique: true,
                filter: "fecha_desmontaje_utc IS NULL AND rol_critico_codigo IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_componentes_unidad_rol_critico",
                table: "componentes_unidad_operativa",
                sql: "rol_critico_codigo IS NULL OR rol_critico_codigo IN ('FABRICA','CHASIS')");

            migrationBuilder.CreateIndex(
                name: "IX_alias_identificador_activo_activo_id_tipo_identificador_vig~",
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
                name: "IX_detalles_matriz_requisitos_documentales_matriz_id_tipo_docu~",
                table: "detalles_matriz_requisitos_documentales",
                columns: new[] { "matriz_id", "tipo_documental_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detalles_matriz_requisitos_documentales_tipo_documental_id",
                table: "detalles_matriz_requisitos_documentales",
                column: "tipo_documental_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalles_ot_documental_activo_id_matriz_detalle_id_clave_ci~",
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
                name: "IX_matrices_requisitos_documentales_codigo_numero_version",
                table: "matrices_requisitos_documentales",
                columns: new[] { "codigo", "numero_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_familia_equipo_id",
                table: "matrices_requisitos_documentales",
                column: "familia_equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_matrices_requisitos_documentales_tipo_activo_id_familia_equ~",
                table: "matrices_requisitos_documentales",
                columns: new[] { "tipo_activo_id", "familia_equipo_id", "vigencia_desde" });

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

            migrationBuilder.AddForeignKey(
                name: "fk_ot_matriz_documental",
                table: "ordenes_trabajo_sql",
                column: "matriz_documental_version_id",
                principalTable: "matrices_requisitos_documentales",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_unidades_operativas_activos_estado_derivado_por_activo_id",
                table: "unidades_operativas",
                column: "estado_derivado_por_activo_id",
                principalTable: "activos",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_unidades_operativas_estados_operacionales_activo_estado_ope~",
                table: "unidades_operativas",
                column: "estado_operacional_base_id",
                principalTable: "estados_operacionales_activo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_unidades_operativas_estados_operacionales_activo_estado_op~1",
                table: "unidades_operativas",
                column: "estado_operacional_id",
                principalTable: "estados_operacionales_activo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_versiones_documento_versiones_documento_reemplaza_version_id",
                table: "versiones_documento",
                column: "reemplaza_version_id",
                principalTable: "versiones_documento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ux_activos_numero_serie_ambito;
                DROP FUNCTION IF EXISTS cmms_set_critical_role() CASCADE;
                DROP FUNCTION IF EXISTS cmms_enforce_unique_asset_attribute() CASCADE;
                DROP FUNCTION IF EXISTS cmms_require_asset_state_event() CASCADE;
                DROP FUNCTION IF EXISTS cmms_require_asset_transfer() CASCADE;
                DROP FUNCTION IF EXISTS cmms_prevent_critical_delete() CASCADE;
                """);
            migrationBuilder.DropForeignKey(
                name: "fk_ot_matriz_documental",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropForeignKey(
                name: "FK_unidades_operativas_activos_estado_derivado_por_activo_id",
                table: "unidades_operativas");

            migrationBuilder.DropForeignKey(
                name: "FK_unidades_operativas_estados_operacionales_activo_estado_ope~",
                table: "unidades_operativas");

            migrationBuilder.DropForeignKey(
                name: "FK_unidades_operativas_estados_operacionales_activo_estado_op~1",
                table: "unidades_operativas");

            migrationBuilder.DropForeignKey(
                name: "FK_versiones_documento_versiones_documento_reemplaza_version_id",
                table: "versiones_documento");

            migrationBuilder.DropTable(
                name: "alias_identificador_activo");

            migrationBuilder.DropTable(
                name: "detalles_ot_documental");

            migrationBuilder.DropTable(
                name: "vigencias_ubicacion_activo");

            migrationBuilder.DropTable(
                name: "detalles_matriz_requisitos_documentales");

            migrationBuilder.DropTable(
                name: "traslados_activo");

            migrationBuilder.DropTable(
                name: "matrices_requisitos_documentales");

            migrationBuilder.DropIndex(
                name: "IX_versiones_documento_reemplaza_version_id",
                table: "versiones_documento");

            migrationBuilder.DropIndex(
                name: "IX_unidades_operativas_estado_derivado_por_activo_id",
                table: "unidades_operativas");

            migrationBuilder.DropIndex(
                name: "IX_unidades_operativas_estado_operacional_base_id",
                table: "unidades_operativas");

            migrationBuilder.DropIndex(
                name: "IX_ordenes_trabajo_sql_matriz_documental_version_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropIndex(
                name: "IX_componentes_unidad_operativa_activo_id",
                table: "componentes_unidad_operativa");

            migrationBuilder.DropIndex(
                name: "IX_componentes_unidad_operativa_unidad_operativa_id_rol_critic~",
                table: "componentes_unidad_operativa");

            migrationBuilder.DropCheckConstraint(
                name: "ck_componentes_unidad_rol_critico",
                table: "componentes_unidad_operativa");

            migrationBuilder.DropColumn(
                name: "ciclo_correccion_id",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "estado_correccion",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "estado_validacion",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "fecha_emision",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "fecha_vencimiento",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "motivo_rechazo",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "observacion_correccion",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "rechazado_por_usuario_id",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "rechazado_utc",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "reemplaza_version_id",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "responsable_correccion_usuario_id",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "validado_por_usuario_id",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "validado_utc",
                table: "versiones_documento");

            migrationBuilder.DropColumn(
                name: "estado_derivado_calculado_utc",
                table: "unidades_operativas");

            migrationBuilder.DropColumn(
                name: "estado_derivado_por_activo_id",
                table: "unidades_operativas");

            migrationBuilder.DropColumn(
                name: "estado_operacional_base_id",
                table: "unidades_operativas");

            migrationBuilder.DropColumn(
                name: "motivo_estado_derivado",
                table: "unidades_operativas");

            migrationBuilder.DropColumn(
                name: "critico",
                table: "roles_componente_unidad");

            migrationBuilder.DropColumn(
                name: "matriz_documental_version_id",
                table: "ordenes_trabajo_sql");

            migrationBuilder.DropColumn(
                name: "antecedente_id",
                table: "eventos_estado_activo");

            migrationBuilder.DropColumn(
                name: "tipo_antecedente",
                table: "eventos_estado_activo");

            migrationBuilder.DropColumn(
                name: "severidad",
                table: "estados_operacionales_activo");

            migrationBuilder.DropColumn(
                name: "desmontado_por_usuario_id",
                table: "componentes_unidad_operativa");

            migrationBuilder.DropColumn(
                name: "montado_por_usuario_id",
                table: "componentes_unidad_operativa");

            migrationBuilder.DropColumn(
                name: "motivo_desmontaje",
                table: "componentes_unidad_operativa");

            migrationBuilder.DropColumn(
                name: "motivo_montaje",
                table: "componentes_unidad_operativa");

            migrationBuilder.DropColumn(
                name: "rol_critico_codigo",
                table: "componentes_unidad_operativa");

            migrationBuilder.CreateIndex(
                name: "IX_componentes_unidad_operativa_activo_id_fecha_desmontaje_utc",
                table: "componentes_unidad_operativa",
                columns: new[] { "activo_id", "fecha_desmontaje_utc" },
                unique: true,
                filter: "fecha_desmontaje_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_componentes_unidad_operativa_unidad_operativa_id",
                table: "componentes_unidad_operativa",
                column: "unidad_operativa_id");

            migrationBuilder.AddForeignKey(
                name: "FK_unidades_operativas_estados_operacionales_activo_estado_ope~",
                table: "unidades_operativas",
                column: "estado_operacional_id",
                principalTable: "estados_operacionales_activo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}


