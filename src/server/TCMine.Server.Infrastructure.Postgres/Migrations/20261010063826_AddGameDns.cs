using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddGameDns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CloudflareApiTokenEncrypted",
                table: "installation_settings",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CloudflareZoneId",
                table: "installation_settings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DnsBaseDomain",
                table: "installation_settings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DnsHostLabel",
                table: "installation_settings",
                type: "character varying(63)",
                maxLength: 63,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subdomain",
                table: "game_servers",
                type: "character varying(63)",
                maxLength: 63,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_servers_Subdomain",
                table: "game_servers",
                column: "Subdomain",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_game_servers_Subdomain",
                table: "game_servers");

            migrationBuilder.DropColumn(
                name: "CloudflareApiTokenEncrypted",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "CloudflareZoneId",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "DnsBaseDomain",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "DnsHostLabel",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "Subdomain",
                table: "game_servers");
        }
    }
}
