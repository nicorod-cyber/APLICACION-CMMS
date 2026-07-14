using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AssetOperationalModelRefactoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_activos_estado_registro",
                table: "activos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_estados_operacionales_activo_codigo",
                table: "estados_operacionales_activo");

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "familias_equipo",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "marca_referencia",
                table: "familias_equipo",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modelo_referencia",
                table: "familias_equipo",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tipo_activo_id",
                table: "familias_equipo",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "propiedad",
                table: "activos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "numero_serie",
                table: "activos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "familia_equipo_id",
                table: "activos",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "faena_id",
                table: "activos",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "criticidad",
                table: "activos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AddColumn<short>(
                name: "anio_fabricacion",
                table: "activos",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_adquisicion",
                table: "activos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_baja",
                table: "activos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_puesta_servicio",
                table: "activos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observaciones",
                table: "activos",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tipo_activo_id",
                table: "activos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "tipo_medicion_uso",
                table: "activos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ubicacion_tecnica_id",
                table: "activos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lecturas_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_lectura_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    origen = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    orden_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registrado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    evidencia_referencia = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    es_correccion = table.Column<bool>(type: "boolean", nullable: false),
                    lectura_corregida_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_correccion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    autorizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    es_anomala = table.Column<bool>(type: "boolean", nullable: false),
                    mensaje_validacion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "roles_componente_unidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles_componente_unidad", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipos_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    categoria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    es_movil = table.Column<bool>(type: "boolean", nullable: false),
                    es_montable = table.Column<bool>(type: "boolean", nullable: false),
                    puede_ser_portador = table.Column<bool>(type: "boolean", nullable: false),
                    controla_mantenimiento = table.Column<bool>(type: "boolean", nullable: false),
                    participa_en_disponibilidad = table.Column<bool>(type: "boolean", nullable: false),
                    orden_visualizacion = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_activo", x => x.id);
                    table.CheckConstraint("ck_tipos_activo_orden", "orden_visualizacion >= 0");
                });

            migrationBuilder.CreateTable(
                name: "tipos_unidad_operativa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    participa_en_disponibilidad = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_unidad_operativa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "definiciones_atributo_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_equipo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tipo_dato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    es_identificador = table.Column<bool>(type: "boolean", nullable: false),
                    es_unico = table.Column<bool>(type: "boolean", nullable: false),
                    permite_busqueda = table.Column<bool>(type: "boolean", nullable: false),
                    permite_filtro = table.Column<bool>(type: "boolean", nullable: false),
                    mostrar_en_listado = table.Column<bool>(type: "boolean", nullable: false),
                    valor_minimo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    valor_maximo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    patron_validacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    opciones_json = table.Column<string>(type: "jsonb", nullable: true),
                    grupo_visualizacion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    orden_visualizacion = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_definiciones_atributo_activo", x => x.id);
                    table.CheckConstraint("ck_definiciones_atributo_rango", "valor_minimo IS NULL OR valor_maximo IS NULL OR valor_minimo <= valor_maximo");
                    table.CheckConstraint("ck_definiciones_atributo_tipo", "tipo_dato IN ('TEXTO','NUMERO','ENTERO','BOOLEANO','FECHA','OPCION')");
                    table.ForeignKey(
                        name: "FK_definiciones_atributo_activo_familias_equipo_familia_equipo~",
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_equipo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_documental_id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    critico = table.Column<bool>(type: "boolean", nullable: false),
                    bloquea_disponibilidad = table.Column<bool>(type: "boolean", nullable: false),
                    requiere_fecha_vencimiento = table.Column<bool>(type: "boolean", nullable: false),
                    dias_alerta = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requisitos_documentales_tipo_activo", x => x.id);
                    table.CheckConstraint("ck_requisitos_documentales_dias_alerta", "dias_alerta IS NULL OR dias_alerta >= 0");
                    table.ForeignKey(
                        name: "FK_requisitos_documentales_tipo_activo_familias_equipo_familia~",
                        column: x => x.familia_equipo_id,
                        principalTable: "familias_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_requisitos_documentales_tipo_activo_tipos_activo_tipo_activ~",
                        column: x => x.tipo_activo_id,
                        principalTable: "tipos_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_requisitos_documentales_tipo_activo_tipos_documentales_tipo~",
                        column: x => x.tipo_documental_id,
                        principalTable: "tipos_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reglas_composicion_unidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_unidad_operativa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_componente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_minima = table.Column<int>(type: "integer", nullable: false),
                    cantidad_maxima = table.Column<int>(type: "integer", nullable: false),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reglas_composicion_unidad", x => x.id);
                    table.CheckConstraint("ck_reglas_composicion_cantidades", "cantidad_minima >= 0 AND cantidad_maxima >= cantidad_minima");
                    table.CheckConstraint("ck_reglas_composicion_obligatorio", "(obligatorio AND cantidad_minima > 0) OR (NOT obligatorio AND cantidad_minima = 0)");
                    table.ForeignKey(
                        name: "FK_reglas_composicion_unidad_roles_componente_unidad_rol_compo~",
                        column: x => x.rol_componente_id,
                        principalTable: "roles_componente_unidad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reglas_composicion_unidad_tipos_unidad_operativa_tipo_unida~",
                        column: x => x.tipo_unidad_operativa_id,
                        principalTable: "tipos_unidad_operativa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unidades_operativas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    tipo_unidad_operativa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ubicacion_tecnica_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado_operacional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criticidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    fecha_puesta_servicio = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_baja = table.Column<DateOnly>(type: "date", nullable: true),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidades_operativas", x => x.id);
                    table.CheckConstraint("ck_unidades_operativas_fecha_baja", "fecha_baja IS NULL OR fecha_puesta_servicio IS NULL OR fecha_baja >= fecha_puesta_servicio");
                    table.ForeignKey(
                        name: "FK_unidades_operativas_estados_operacionales_activo_estado_ope~",
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
                        name: "FK_unidades_operativas_tipos_unidad_operativa_tipo_unidad_oper~",
                        column: x => x.tipo_unidad_operativa_id,
                        principalTable: "tipos_unidad_operativa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_unidades_operativas_ubicaciones_tecnicas_ubicacion_tecnica_~",
                        column: x => x.ubicacion_tecnica_id,
                        principalTable: "ubicaciones_tecnicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "valores_atributo_activo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_atributo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    valor_numerico = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    valor_booleano = table.Column<bool>(type: "boolean", nullable: true),
                    valor_fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_valores_atributo_activo_definiciones_atributo_activo_defini~",
                        column: x => x.definicion_atributo_id,
                        principalTable: "definiciones_atributo_activo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "componentes_unidad_operativa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_operativa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_componente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_montaje_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fecha_desmontaje_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    orden_trabajo_montaje_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden_trabajo_desmontaje_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_componentes_unidad_operativa", x => x.id);
                    table.CheckConstraint("ck_componentes_unidad_fechas", "fecha_desmontaje_utc IS NULL OR fecha_desmontaje_utc >= fecha_montaje_utc");
                    table.ForeignKey(
                        name: "FK_componentes_unidad_operativa_activos_activo_id",
                        column: x => x.activo_id,
                        principalTable: "activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_componentes_unidad_operativa_ordenes_trabajo_sql_orden_trab~",
                        column: x => x.orden_trabajo_montaje_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_componentes_unidad_operativa_ordenes_trabajo_sql_orden_tra~1",
                        column: x => x.orden_trabajo_desmontaje_id,
                        principalTable: "ordenes_trabajo_sql",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_componentes_unidad_operativa_roles_componente_unidad_rol_co~",
                        column: x => x.rol_componente_id,
                        principalTable: "roles_componente_unidad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_componentes_unidad_operativa_unidades_operativas_unidad_ope~",
                        column: x => x.unidad_operativa_id,
                        principalTable: "unidades_operativas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_familias_equipo_tipo_activo_id",
                table: "familias_equipo",
                column: "tipo_activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_activos_tipo_activo_id",
                table: "activos",
                column: "tipo_activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_activos_ubicacion_tecnica_id",
                table: "activos",
                column: "ubicacion_tecnica_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activos_anio_fabricacion",
                table: "activos",
                sql: "anio_fabricacion IS NULL OR anio_fabricacion BETWEEN 1900 AND 2200");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activos_fecha_baja",
                table: "activos",
                sql: "fecha_baja IS NULL OR fecha_puesta_servicio IS NULL OR fecha_baja >= fecha_puesta_servicio");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activos_tipo_medicion_uso",
                table: "activos",
                sql: "tipo_medicion_uso IS NULL OR tipo_medicion_uso IN ('HOROMETRO','KILOMETRAJE')");

            migrationBuilder.CreateIndex(
                name: "IX_componentes_unidad_operativa_activo_id_fecha_desmontaje_utc",
                table: "componentes_unidad_operativa",
                columns: new[] { "activo_id", "fecha_desmontaje_utc" },
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
                name: "IX_componentes_unidad_operativa_unidad_operativa_id",
                table: "componentes_unidad_operativa",
                column: "unidad_operativa_id");

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
                name: "IX_definiciones_atributo_activo_tipo_activo_id_familia_equipo_~",
                table: "definiciones_atributo_activo",
                columns: new[] { "tipo_activo_id", "familia_equipo_id", "codigo" },
                unique: true,
                filter: "familia_equipo_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_lecturas_activo_activo_id_fecha_lectura_utc",
                table: "lecturas_activo",
                columns: new[] { "activo_id", "fecha_lectura_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_lecturas_activo_lectura_corregida_id",
                table: "lecturas_activo",
                column: "lectura_corregida_id");

            migrationBuilder.CreateIndex(
                name: "IX_lecturas_activo_orden_trabajo_id",
                table: "lecturas_activo",
                column: "orden_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "IX_reglas_composicion_unidad_rol_componente_id",
                table: "reglas_composicion_unidad",
                column: "rol_componente_id");

            migrationBuilder.CreateIndex(
                name: "IX_reglas_composicion_unidad_tipo_unidad_operativa_id_rol_comp~",
                table: "reglas_composicion_unidad",
                columns: new[] { "tipo_unidad_operativa_id", "rol_componente_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_requisitos_documentales_tipo_activo_familia_equipo_id",
                table: "requisitos_documentales_tipo_activo",
                column: "familia_equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_requisitos_documentales_tipo_activo_tipo_activo_id_familia_~",
                table: "requisitos_documentales_tipo_activo",
                columns: new[] { "tipo_activo_id", "familia_equipo_id", "tipo_documental_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_requisitos_documentales_tipo_activo_tipo_documental_id",
                table: "requisitos_documentales_tipo_activo",
                column: "tipo_documental_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_componente_unidad_codigo",
                table: "roles_componente_unidad",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipos_activo_codigo",
                table: "tipos_activo",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipos_unidad_operativa_codigo",
                table: "tipos_unidad_operativa",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidades_operativas_codigo",
                table: "unidades_operativas",
                column: "codigo",
                unique: true);

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
                name: "IX_unidades_operativas_ubicacion_tecnica_id",
                table: "unidades_operativas",
                column: "ubicacion_tecnica_id");

            migrationBuilder.CreateIndex(
                name: "IX_valores_atributo_activo_activo_id_definicion_atributo_id",
                table: "valores_atributo_activo",
                columns: new[] { "activo_id", "definicion_atributo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_valores_atributo_activo_definicion_atributo_id",
                table: "valores_atributo_activo",
                column: "definicion_atributo_id");

            // Data preservation: normalize legacy free-text types, locations and plates before removing legacy columns.
            migrationBuilder.Sql(@"
                INSERT INTO tipos_activo (id, codigo, nombre, categoria, es_movil, es_montable, puede_ser_portador, controla_mantenimiento, participa_en_disponibilidad, orden_visualizacion, activo, created_at_utc)
                SELECT gen_random_uuid(), codigo, nombre, 'LEGADO', false, false, false, true, true, 0, true, now()
                FROM (
                    SELECT DISTINCT upper(regexp_replace(trim(tipo_activo), '[^A-Za-z0-9]+', '_', 'g')) AS codigo, trim(tipo_activo) AS nombre
                    FROM activos WHERE nullif(trim(tipo_activo), '') IS NOT NULL
                ) legacy
                ON CONFLICT (codigo) DO NOTHING;

                INSERT INTO tipos_activo (id, codigo, nombre, categoria, es_movil, es_montable, puede_ser_portador, controla_mantenimiento, participa_en_disponibilidad, orden_visualizacion, activo, created_at_utc)
                SELECT gen_random_uuid(), 'SIN_CLASIFICAR', 'Sin clasificar', 'LEGADO', false, false, false, true, true, 9999, true, now()
                WHERE NOT EXISTS (SELECT 1 FROM tipos_activo WHERE codigo = 'SIN_CLASIFICAR');

                UPDATE activos a
                SET tipo_activo_id = t.id
                FROM tipos_activo t
                WHERE t.codigo = coalesce(nullif(upper(regexp_replace(trim(a.tipo_activo), '[^A-Za-z0-9]+', '_', 'g')), ''), 'SIN_CLASIFICAR');

                UPDATE familias_equipo f
                SET tipo_activo_id = coalesce((SELECT a.tipo_activo_id FROM activos a WHERE a.familia_equipo_id = f.id GROUP BY a.tipo_activo_id ORDER BY count(*) DESC LIMIT 1), (SELECT id FROM tipos_activo WHERE codigo = 'SIN_CLASIFICAR'));

                UPDATE activos a
                SET ubicacion_tecnica_id = u.id
                FROM ubicaciones_tecnicas u
                WHERE upper(trim(u.codigo)) = upper(trim(a.ubicacion_tecnica_codigo));

                UPDATE activos a
                SET observaciones = concat_ws(E'\n', nullif(observaciones, ''), CASE WHEN nullif(trim(ubicacion_tecnica_codigo), '') IS NOT NULL AND ubicacion_tecnica_id IS NULL THEN '[MIGRACION] ubicacion tecnica legado no encontrada: ' || ubicacion_tecnica_codigo END)
                WHERE nullif(trim(ubicacion_tecnica_codigo), '') IS NOT NULL;

                INSERT INTO definiciones_atributo_activo (id, tipo_activo_id, codigo, nombre, tipo_dato, obligatorio, es_identificador, es_unico, permite_busqueda, permite_filtro, mostrar_en_listado, orden_visualizacion, activo, created_at_utc)
                SELECT gen_random_uuid(), a.tipo_activo_id, 'PATENTE', 'Patente', 'TEXTO', false, true, false, true, true, true, 0, true, now()
                FROM activos a WHERE nullif(trim(a.patente), '') IS NOT NULL
                GROUP BY a.tipo_activo_id
                ON CONFLICT DO NOTHING;

                INSERT INTO valores_atributo_activo (id, activo_id, definicion_atributo_id, valor_texto, created_at_utc)
                SELECT gen_random_uuid(), a.id, d.id, a.patente, now()
                FROM activos a JOIN definiciones_atributo_activo d ON d.tipo_activo_id = a.tipo_activo_id AND d.codigo = 'PATENTE' AND d.familia_equipo_id IS NULL
                WHERE nullif(trim(a.patente), '') IS NOT NULL;

                INSERT INTO estados_operacionales_activo (id, codigo, nombre, activo, created_at_utc)
                SELECT gen_random_uuid(), 'DADO_DE_BAJA', 'Dado de baja', true, now()
                WHERE NOT EXISTS (SELECT 1 FROM estados_operacionales_activo WHERE codigo = 'DADO_DE_BAJA');

                UPDATE activos a SET estado_operacional_id = s.id
                FROM estados_operacionales_activo s
                WHERE s.codigo = 'DADO_DE_BAJA' AND lower(coalesce(a.estado_registro, '')) IN ('baja', 'obsoleto', 'no_vigente', 'anulado');
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_activos_tipos_activo_tipo_activo_id",
                table: "activos",
                column: "tipo_activo_id",
                principalTable: "tipos_activo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_activos_ubicaciones_tecnicas_ubicacion_tecnica_id",
                table: "activos",
                column: "ubicacion_tecnica_id",
                principalTable: "ubicaciones_tecnicas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_familias_equipo_tipos_activo_tipo_activo_id",
                table: "familias_equipo",
                column: "tipo_activo_id",
                principalTable: "tipos_activo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.DropColumn(name: "estado_documental", table: "activos");
            migrationBuilder.DropColumn(name: "estado_registro", table: "activos");
            migrationBuilder.DropColumn(name: "ficha_validada", table: "activos");
            migrationBuilder.DropColumn(name: "patente", table: "activos");
            migrationBuilder.DropColumn(name: "tipo_activo", table: "activos");
            migrationBuilder.DropColumn(name: "ubicacion_tecnica_codigo", table: "activos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE activos SET estado_operacional_id = (SELECT id FROM estados_operacionales_activo WHERE codigo = 'FUERA_SERVICIO_FAENA' LIMIT 1)
                WHERE estado_operacional_id IN (SELECT id FROM estados_operacionales_activo WHERE codigo = 'DADO_DE_BAJA');
                DELETE FROM estados_operacionales_activo WHERE codigo = 'DADO_DE_BAJA';
            ");
            migrationBuilder.AddCheckConstraint(
                name: "ck_estados_operacionales_activo_codigo",
                table: "estados_operacionales_activo",
                sql: "codigo IN ('OPERATIVO_FAENA','ALERTA_FAENA','FUERA_SERVICIO_FAENA','FUERA_SERVICIO_TALLER')");
            migrationBuilder.DropForeignKey(
                name: "FK_activos_tipos_activo_tipo_activo_id",
                table: "activos");

            migrationBuilder.DropForeignKey(
                name: "FK_activos_ubicaciones_tecnicas_ubicacion_tecnica_id",
                table: "activos");

            migrationBuilder.DropForeignKey(
                name: "FK_familias_equipo_tipos_activo_tipo_activo_id",
                table: "familias_equipo");

            migrationBuilder.DropTable(
                name: "componentes_unidad_operativa");

            migrationBuilder.DropTable(
                name: "lecturas_activo");

            migrationBuilder.DropTable(
                name: "reglas_composicion_unidad");

            migrationBuilder.DropTable(
                name: "requisitos_documentales_tipo_activo");

            migrationBuilder.DropTable(
                name: "valores_atributo_activo");

            migrationBuilder.DropTable(
                name: "unidades_operativas");

            migrationBuilder.DropTable(
                name: "roles_componente_unidad");

            migrationBuilder.DropTable(
                name: "definiciones_atributo_activo");

            migrationBuilder.DropTable(
                name: "tipos_unidad_operativa");

            migrationBuilder.DropTable(
                name: "tipos_activo");

            migrationBuilder.DropIndex(
                name: "IX_familias_equipo_tipo_activo_id",
                table: "familias_equipo");

            migrationBuilder.DropIndex(
                name: "IX_activos_tipo_activo_id",
                table: "activos");

            migrationBuilder.DropIndex(
                name: "IX_activos_ubicacion_tecnica_id",
                table: "activos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activos_anio_fabricacion",
                table: "activos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activos_fecha_baja",
                table: "activos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activos_tipo_medicion_uso",
                table: "activos");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "familias_equipo");

            migrationBuilder.DropColumn(
                name: "marca_referencia",
                table: "familias_equipo");

            migrationBuilder.DropColumn(
                name: "modelo_referencia",
                table: "familias_equipo");

            migrationBuilder.DropColumn(
                name: "tipo_activo_id",
                table: "familias_equipo");

            migrationBuilder.DropColumn(
                name: "anio_fabricacion",
                table: "activos");

            migrationBuilder.DropColumn(
                name: "fecha_adquisicion",
                table: "activos");

            migrationBuilder.DropColumn(
                name: "fecha_baja",
                table: "activos");

            migrationBuilder.DropColumn(
                name: "fecha_puesta_servicio",
                table: "activos");

            migrationBuilder.DropColumn(
                name: "observaciones",
                table: "activos");

            migrationBuilder.DropColumn(
                name: "tipo_activo_id",
                table: "activos");

            migrationBuilder.DropColumn(
                name: "tipo_medicion_uso",
                table: "activos");

            migrationBuilder.DropColumn(
                name: "ubicacion_tecnica_id",
                table: "activos");

            migrationBuilder.AlterColumn<string>(
                name: "propiedad",
                table: "activos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "numero_serie",
                table: "activos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "familia_equipo_id",
                table: "activos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "faena_id",
                table: "activos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "criticidad",
                table: "activos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado_documental",
                table: "activos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado_registro",
                table: "activos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ficha_validada",
                table: "activos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "patente",
                table: "activos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_activo",
                table: "activos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ubicacion_tecnica_codigo",
                table: "activos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_activos_estado_registro",
                table: "activos",
                sql: "estado_registro IN ('vigente','inactivo','anulado','obsoleto','reemplazado','no_vigente')");
        }
    }
}





