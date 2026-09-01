using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class FaenaTechnicalLocationOneToOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A legacy technical node can carry a stale faena while both its parent and
            // its direct location agree on a different faena. This is the only conflict
            // repaired automatically: two independent existing links identify the target.
            migrationBuilder.Sql(
                @"UPDATE nodos_tecnicos AS n
                  SET faena_id = ut.faena_id
                  FROM ubicaciones_tecnicas AS ut,
                       nodos_tecnicos AS parent
                  WHERE n.ubicacion_tecnica_id = ut.id
                    AND parent.id = n.nodo_padre_id
                    AND n.faena_id IS NOT NULL
                    AND n.faena_id <> ut.faena_id
                    AND parent.faena_id = ut.faena_id;");
            // The direct location links being removed below can only be derived through a
            // faena when they agree with the location's faena. Stop with actionable detail
            // instead of silently discarding an ambiguous association.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    details text;
                BEGIN
                    SELECT string_agg(item.detail, E'\n')
                    INTO details
                    FROM (
                        SELECT format(
                            'Faena %s (%s) has %s technical locations: %s',
                            f.codigo,
                            f.id,
                            count(*),
                            string_agg(format('%s (%s)', ut.codigo, ut.id), ', ' ORDER BY ut.codigo)) AS detail
                        FROM faenas AS f
                        INNER JOIN ubicaciones_tecnicas AS ut ON ut.faena_id = f.id
                        GROUP BY f.id, f.codigo
                        HAVING count(*) > 1
                    ) AS item;

                    IF details IS NOT NULL THEN
                        RAISE EXCEPTION USING
                            MESSAGE = 'FaenaTechnicalLocationOneToOne stopped: resolve faenas with more than one technical location before applying this migration.',
                            DETAIL = details;
                    END IF;

                    SELECT string_agg(item.detail, E'\n')
                    INTO details
                    FROM (
                        SELECT format(
                            '%s %s has faena %s but its technical location %s belongs to faena %s',
                            source,
                            record_id,
                            COALESCE(record_faena_id::text, 'NULL'),
                            COALESCE(technical_location_id::text, 'NULL'),
                            COALESCE(location_faena_id::text, 'NULL')) AS detail
                        FROM (
                            SELECT 'activo' AS source, a.id AS record_id, a.faena_id AS record_faena_id,
                                   a.ubicacion_tecnica_id AS technical_location_id, ut.faena_id AS location_faena_id
                            FROM activos AS a
                            LEFT JOIN ubicaciones_tecnicas AS ut ON ut.id = a.ubicacion_tecnica_id
                            WHERE a.ubicacion_tecnica_id IS NOT NULL
                              AND (ut.faena_id IS NULL OR (a.faena_id IS NOT NULL AND a.faena_id <> ut.faena_id))
                            UNION ALL
                            SELECT 'unidad_operativa', u.id, u.faena_id, u.ubicacion_tecnica_id, ut.faena_id
                            FROM unidades_operativas AS u
                            LEFT JOIN ubicaciones_tecnicas AS ut ON ut.id = u.ubicacion_tecnica_id
                            WHERE u.ubicacion_tecnica_id IS NOT NULL
                              AND (ut.faena_id IS NULL OR (u.faena_id IS NOT NULL AND u.faena_id <> ut.faena_id))
                            UNION ALL
                            SELECT 'nodo_tecnico', n.id, n.faena_id, n.ubicacion_tecnica_id, ut.faena_id
                            FROM nodos_tecnicos AS n
                            LEFT JOIN ubicaciones_tecnicas AS ut ON ut.id = n.ubicacion_tecnica_id
                            WHERE n.ubicacion_tecnica_id IS NOT NULL
                              AND (ut.faena_id IS NULL OR (n.faena_id IS NOT NULL AND n.faena_id <> ut.faena_id))
                        ) AS conflicts
                    ) AS item;

                    IF details IS NOT NULL THEN
                        RAISE EXCEPTION USING
                            MESSAGE = 'FaenaTechnicalLocationOneToOne stopped: direct technical-location links conflict with their faena.',
                            DETAIL = details;
                    END IF;
                END $$;
                """);

            // A null faena with an existing direct technical location is an unambiguous
            // legacy case. Preserve it before deleting the direct foreign-key columns.
            migrationBuilder.Sql(
                """
                UPDATE activos AS a
                SET faena_id = ut.faena_id
                FROM ubicaciones_tecnicas AS ut
                WHERE a.faena_id IS NULL
                  AND a.ubicacion_tecnica_id = ut.id;

                UPDATE unidades_operativas AS u
                SET faena_id = ut.faena_id
                FROM ubicaciones_tecnicas AS ut
                WHERE u.faena_id IS NULL
                  AND u.ubicacion_tecnica_id = ut.id;

                UPDATE nodos_tecnicos AS n
                SET faena_id = ut.faena_id
                FROM ubicaciones_tecnicas AS ut
                WHERE n.faena_id IS NULL
                  AND n.ubicacion_tecnica_id = ut.id;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_activos_ubicaciones_tecnicas_ubicacion_tecnica_id",
                table: "activos");

            migrationBuilder.DropForeignKey(
                name: "FK_nodos_tecnicos_ubicaciones_tecnicas_ubicacion_tecnica_id",
                table: "nodos_tecnicos");

            migrationBuilder.DropForeignKey(
                name: "FK_ubicaciones_tecnicas_ubicaciones_tecnicas_ubicacion_padre_id",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropForeignKey(
                name: "FK_unidades_operativas_ubicaciones_tecnicas_ubicacion_tecnica_~",
                table: "unidades_operativas");

            migrationBuilder.DropIndex(
                name: "IX_unidades_operativas_ubicacion_tecnica_id",
                table: "unidades_operativas");

            migrationBuilder.DropIndex(
                name: "IX_ubicaciones_tecnicas_faena_id",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropIndex(
                name: "IX_ubicaciones_tecnicas_nombre_normalizado",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropIndex(
                name: "IX_ubicaciones_tecnicas_ubicacion_padre_id",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ubicaciones_tecnicas_no_self_parent",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropIndex(
                name: "IX_nodos_tecnicos_ubicacion_tecnica_id",
                table: "nodos_tecnicos");

            migrationBuilder.DropIndex(
                name: "IX_activos_ubicacion_tecnica_id",
                table: "activos");

            migrationBuilder.DropColumn(
                name: "ubicacion_tecnica_id",
                table: "unidades_operativas");

            migrationBuilder.DropColumn(
                name: "actualizado_por_usuario_id",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropColumn(
                name: "creado_por_usuario_id",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropColumn(
                name: "nombre_normalizado",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropColumn(
                name: "ubicacion_padre_id",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropColumn(
                name: "ubicacion_tecnica_id",
                table: "nodos_tecnicos");

            migrationBuilder.DropColumn(
                name: "ubicacion_tecnica_id",
                table: "activos");

            migrationBuilder.AddColumn<string>(
                name: "centro_costes",
                table: "faenas",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cliente",
                table: "faenas",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "comuna",
                table: "faenas",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "latitud",
                table: "faenas",
                type: "numeric(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitud",
                table: "faenas",
                type: "numeric(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "region",
                table: "faenas",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "responsable_usuario_id",
                table: "faenas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_faena",
                table: "faenas",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zona",
                table: "faenas",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_faena_id",
                table: "ubicaciones_tecnicas",
                column: "faena_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_faenas_responsable_usuario_id",
                table: "faenas",
                column: "responsable_usuario_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_faenas_latitud",
                table: "faenas",
                sql: "latitud IS NULL OR latitud BETWEEN -90 AND 90");

            migrationBuilder.AddCheckConstraint(
                name: "ck_faenas_longitud",
                table: "faenas",
                sql: "longitud IS NULL OR longitud BETWEEN -180 AND 180");

            migrationBuilder.AddForeignKey(
                name: "FK_faenas_usuarios_responsable_usuario_id",
                table: "faenas",
                column: "responsable_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_faenas_usuarios_responsable_usuario_id",
                table: "faenas");

            migrationBuilder.DropIndex(
                name: "IX_ubicaciones_tecnicas_faena_id",
                table: "ubicaciones_tecnicas");

            migrationBuilder.DropIndex(
                name: "IX_faenas_responsable_usuario_id",
                table: "faenas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_faenas_latitud",
                table: "faenas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_faenas_longitud",
                table: "faenas");

            migrationBuilder.DropColumn(
                name: "centro_costes",
                table: "faenas");

            migrationBuilder.DropColumn(
                name: "cliente",
                table: "faenas");

            migrationBuilder.DropColumn(
                name: "comuna",
                table: "faenas");

            migrationBuilder.DropColumn(
                name: "latitud",
                table: "faenas");

            migrationBuilder.DropColumn(
                name: "longitud",
                table: "faenas");

            migrationBuilder.DropColumn(
                name: "region",
                table: "faenas");

            migrationBuilder.DropColumn(
                name: "responsable_usuario_id",
                table: "faenas");

            migrationBuilder.DropColumn(
                name: "tipo_faena",
                table: "faenas");

            migrationBuilder.DropColumn(
                name: "zona",
                table: "faenas");

            migrationBuilder.AddColumn<Guid>(
                name: "ubicacion_tecnica_id",
                table: "unidades_operativas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actualizado_por_usuario_id",
                table: "ubicaciones_tecnicas",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "creado_por_usuario_id",
                table: "ubicaciones_tecnicas",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nombre_normalizado",
                table: "ubicaciones_tecnicas",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tipo",
                table: "ubicaciones_tecnicas",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ubicacion_padre_id",
                table: "ubicaciones_tecnicas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ubicacion_tecnica_id",
                table: "nodos_tecnicos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ubicacion_tecnica_id",
                table: "activos",
                type: "uuid",
                nullable: true);
            // This migration's Up path is safe because a faena has at most one location.
            // Restore the former direct links deterministically if the migration is rolled back.
            migrationBuilder.Sql(
                """
                UPDATE activos AS a
                SET ubicacion_tecnica_id = ut.id
                FROM ubicaciones_tecnicas AS ut
                WHERE ut.faena_id = a.faena_id;

                UPDATE unidades_operativas AS u
                SET ubicacion_tecnica_id = ut.id
                FROM ubicaciones_tecnicas AS ut
                WHERE ut.faena_id = u.faena_id;

                UPDATE nodos_tecnicos AS n
                SET ubicacion_tecnica_id = ut.id
                FROM ubicaciones_tecnicas AS ut
                WHERE ut.faena_id = n.faena_id;
                """);
            migrationBuilder.CreateIndex(
                name: "IX_unidades_operativas_ubicacion_tecnica_id",
                table: "unidades_operativas",
                column: "ubicacion_tecnica_id");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_faena_id",
                table: "ubicaciones_tecnicas",
                column: "faena_id");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_nombre_normalizado",
                table: "ubicaciones_tecnicas",
                column: "nombre_normalizado");

            migrationBuilder.CreateIndex(
                name: "IX_ubicaciones_tecnicas_ubicacion_padre_id",
                table: "ubicaciones_tecnicas",
                column: "ubicacion_padre_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ubicaciones_tecnicas_no_self_parent",
                table: "ubicaciones_tecnicas",
                sql: "ubicacion_padre_id IS NULL OR ubicacion_padre_id <> id");

            migrationBuilder.CreateIndex(
                name: "IX_nodos_tecnicos_ubicacion_tecnica_id",
                table: "nodos_tecnicos",
                column: "ubicacion_tecnica_id");

            migrationBuilder.CreateIndex(
                name: "IX_activos_ubicacion_tecnica_id",
                table: "activos",
                column: "ubicacion_tecnica_id");

            migrationBuilder.AddForeignKey(
                name: "FK_activos_ubicaciones_tecnicas_ubicacion_tecnica_id",
                table: "activos",
                column: "ubicacion_tecnica_id",
                principalTable: "ubicaciones_tecnicas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_nodos_tecnicos_ubicaciones_tecnicas_ubicacion_tecnica_id",
                table: "nodos_tecnicos",
                column: "ubicacion_tecnica_id",
                principalTable: "ubicaciones_tecnicas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ubicaciones_tecnicas_ubicaciones_tecnicas_ubicacion_padre_id",
                table: "ubicaciones_tecnicas",
                column: "ubicacion_padre_id",
                principalTable: "ubicaciones_tecnicas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_unidades_operativas_ubicaciones_tecnicas_ubicacion_tecnica_~",
                table: "unidades_operativas",
                column: "ubicacion_tecnica_id",
                principalTable: "ubicaciones_tecnicas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
