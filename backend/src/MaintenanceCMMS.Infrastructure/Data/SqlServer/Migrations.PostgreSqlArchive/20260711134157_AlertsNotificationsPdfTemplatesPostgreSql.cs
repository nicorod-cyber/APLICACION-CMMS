using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AlertsNotificationsPdfTemplatesPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plantillas_pdf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    tipo_evento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    asunto_plantilla = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    html_plantilla = table.Column<string>(type: "text", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    version_plantilla = table.Column<int>(type: "integer", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    archivo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "reglas_alerta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    tipo_evento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    severidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    repetir_hasta_resolver = table.Column<bool>(type: "boolean", nullable: false),
                    genera_email = table.Column<bool>(type: "boolean", nullable: false),
                    genera_pdf = table.Column<bool>(type: "boolean", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uuid", nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "alertas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    regla_alerta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    mensaje = table.Column<string>(type: "text", nullable: false),
                    severidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origen = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    clave_causa = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    clave_deduplicacion = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_entidad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    entidad_id = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    repeticion_critica = table.Column<bool>(type: "boolean", nullable: false),
                    cantidad_repeticiones = table.Column<int>(type: "integer", nullable: false),
                    reconocido_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reconocido_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    resuelto_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    resuelto_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    motivo_resolucion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    archivo_pdf_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    regla_alerta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: true),
                    destino = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    canal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "notificaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alerta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    asunto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    cuerpo = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    programado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    enviado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cantidad_intentos = table.Column<int>(type: "integer", nullable: false),
                    proveedor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ultimo_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    archivo_pdf_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "notificacion_destinatarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notificacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: true),
                    destino = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    estado_entrega = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notificacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_intento = table.Column<int>(type: "integer", nullable: false),
                    intentado_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    exitoso = table.Column<bool>(type: "boolean", nullable: false),
                    proveedor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                filter: "activa");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_tipo_entidad_entidad_id",
                table: "alertas",
                columns: new[] { "tipo_entidad", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_destinatarios_notificacion_id_destino",
                table: "notificacion_destinatarios",
                columns: new[] { "notificacion_id", "destino" },
                unique: true);

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
                name: "IX_regla_alerta_destinatarios_regla_alerta_id_destino_canal",
                table: "regla_alerta_destinatarios",
                columns: new[] { "regla_alerta_id", "destino", "canal" },
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificacion_destinatarios");

            migrationBuilder.DropTable(
                name: "notificacion_intentos");

            migrationBuilder.DropTable(
                name: "regla_alerta_destinatarios");

            migrationBuilder.DropTable(
                name: "notificaciones");

            migrationBuilder.DropTable(
                name: "alertas");

            migrationBuilder.DropTable(
                name: "reglas_alerta");

            migrationBuilder.DropTable(
                name: "plantillas_pdf");
        }
    }
}
