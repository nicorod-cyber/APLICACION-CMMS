using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class TechnicalHierarchyDomainPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ubicaciones_tecnicas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    nombre_normalizado = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ubicacion_padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    obsoleto = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ubicaciones_tecnicas", x => x.id);
                    table.CheckConstraint("ck_ubicaciones_tecnicas_no_self_parent", "ubicacion_padre_id IS NULL OR ubicacion_padre_id <> id");
                    table.ForeignKey(
                        name: "FK_ubicaciones_tecnicas_faenas_faena_id",
                        column: x => x.faena_id,
                        principalTable: "faenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ubicaciones_tecnicas_ubicaciones_tecnicas_ubicacion_padre_id",
                        column: x => x.ubicacion_padre_id,
                        principalTable: "ubicaciones_tecnicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nodos_tecnicos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    nombre_normalizado = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    nivel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nodo_padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    faena_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ubicacion_tecnica_id = table.Column<Guid>(type: "uuid", nullable: true),
                    obsoleto = table.Column<bool>(type: "boolean", nullable: false),
                    fusionado_en_nodo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    actualizado_por_usuario_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    table.ForeignKey(
                        name: "FK_nodos_tecnicos_ubicaciones_tecnicas_ubicacion_tecnica_id",
                        column: x => x.ubicacion_tecnica_id,
                        principalTable: "ubicaciones_tecnicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nodo_tecnico_activos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nodo_tecnico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nodo_tecnico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    alias_normalizado = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    origen = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nodo_tecnico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "IX_nodos_tecnicos_ubicacion_tecnica_id",
                table: "nodos_tecnicos",
                column: "ubicacion_tecnica_id");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_codigo",
                table: "ubicaciones_tecnicas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_faena_id",
                table: "ubicaciones_tecnicas",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_nombre",
                table: "ubicaciones_tecnicas",
                column: "nombre");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_nombre_normalizado",
                table: "ubicaciones_tecnicas",
                column: "nombre_normalizado");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_obsoleto",
                table: "ubicaciones_tecnicas",
                column: "obsoleto");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_ubicacion_padre_id",
                table: "ubicaciones_tecnicas",
                column: "ubicacion_padre_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nodo_tecnico_activos");

            migrationBuilder.DropTable(
                name: "nodo_tecnico_aliases");

            migrationBuilder.DropTable(
                name: "nodo_tecnico_familias");

            migrationBuilder.DropTable(
                name: "nodos_tecnicos");

            migrationBuilder.DropTable(
                name: "ubicaciones_tecnicas");
        }
    }
}
