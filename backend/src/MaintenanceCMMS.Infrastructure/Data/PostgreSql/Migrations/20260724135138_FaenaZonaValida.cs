using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class FaenaZonaValida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE public.faenas
                DROP CONSTRAINT IF EXISTS ck_faenas_zona_valida;

                ALTER TABLE public.faenas
                ADD CONSTRAINT ck_faenas_zona_valida
                CHECK (zona IS NULL OR zona IN ('Zona 0', 'Zona 1', 'Zona 2', 'Zona 3', 'Zona 4'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE public.faenas
                DROP CONSTRAINT IF EXISTS ck_faenas_zona_valida;
                """);
        }
    }
}
