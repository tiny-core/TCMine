using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Server.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ModpackCurseForgeUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurseForgeUrl",
                table: "Modpacks",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurseForgeUrl",
                table: "Modpacks");
        }
    }
}
