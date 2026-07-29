using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class MoveMinecraftAndLoaderToModpack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Loader",
                table: "modpack_versions");

            migrationBuilder.DropColumn(
                name: "MinecraftVersion",
                table: "modpack_versions");

            migrationBuilder.AddColumn<string>(
                name: "Loader",
                table: "modpacks",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MinecraftVersion",
                table: "modpacks",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Loader",
                table: "modpacks");

            migrationBuilder.DropColumn(
                name: "MinecraftVersion",
                table: "modpacks");

            migrationBuilder.AddColumn<string>(
                name: "Loader",
                table: "modpack_versions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MinecraftVersion",
                table: "modpack_versions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }
    }
}
