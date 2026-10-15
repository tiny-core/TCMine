using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundSwitches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PublicIpDetectionDisabled",
                table: "installation_settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateCheckDisabled",
                table: "installation_settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicIpDetectionDisabled",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "UpdateCheckDisabled",
                table: "installation_settings");
        }
    }
}
