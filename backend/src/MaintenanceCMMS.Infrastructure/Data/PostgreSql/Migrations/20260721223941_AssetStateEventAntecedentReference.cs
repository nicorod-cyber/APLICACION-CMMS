using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AssetStateEventAntecedentReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "referencia_antecedente",
                table: "eventos_estado_activo",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "referencia_antecedente",
                table: "eventos_estado_activo");
        }
    }
}
