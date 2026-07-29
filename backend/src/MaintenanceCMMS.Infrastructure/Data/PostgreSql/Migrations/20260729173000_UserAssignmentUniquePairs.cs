using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations;

[DbContext(typeof(CmmsDbContext))]
[Migration("20260729173000_UserAssignmentUniquePairs")]
public partial class UserAssignmentUniquePairs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM usuario_roles
                    GROUP BY usuario_id, rol_id
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'No se puede aplicar la restricción única de usuario y rol porque existen asignaciones históricas duplicadas.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM usuario_faenas
                    GROUP BY usuario_id, faena_id
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'No se puede aplicar la restricción única de usuario y faena porque existen asignaciones históricas duplicadas.';
                END IF;
            END $$;
            """);

        migrationBuilder.DropIndex(
            name: "ix_usuario_roles_usuario_id_rol_id_vigente",
            table: "usuario_roles");

        migrationBuilder.DropIndex(
            name: "ix_usuario_faenas_usuario_id_faena_id_vigente",
            table: "usuario_faenas");

        migrationBuilder.CreateIndex(
            name: "ix_usuario_roles_usuario_id_rol_id",
            table: "usuario_roles",
            columns: new[] { "usuario_id", "rol_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_usuario_faenas_usuario_id_faena_id",
            table: "usuario_faenas",
            columns: new[] { "usuario_id", "faena_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_usuario_roles_usuario_id_rol_id",
            table: "usuario_roles");

        migrationBuilder.DropIndex(
            name: "ix_usuario_faenas_usuario_id_faena_id",
            table: "usuario_faenas");

        migrationBuilder.CreateIndex(
            name: "ix_usuario_roles_usuario_id_rol_id_vigente",
            table: "usuario_roles",
            columns: new[] { "usuario_id", "rol_id", "vigente" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_usuario_faenas_usuario_id_faena_id_vigente",
            table: "usuario_faenas",
            columns: new[] { "usuario_id", "faena_id", "vigente" },
            unique: true);
    }
}
