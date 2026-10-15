using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudflareTunnel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CloudflareAccountId",
                table: "installation_settings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CloudflareTunnelId",
                table: "installation_settings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TunnelOriginHost",
                table: "installation_settings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TunnelLabel",
                table: "game_servers",
                type: "character varying(63)",
                maxLength: 63,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_servers_TunnelLabel",
                table: "game_servers",
                column: "TunnelLabel",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_game_servers_TunnelLabel",
                table: "game_servers");

            migrationBuilder.DropColumn(
                name: "CloudflareAccountId",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "CloudflareTunnelId",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "TunnelOriginHost",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "TunnelLabel",
                table: "game_servers");
        }
    }
}
