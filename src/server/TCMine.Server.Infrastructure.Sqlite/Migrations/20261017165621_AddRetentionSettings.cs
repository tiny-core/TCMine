using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivityRetentionDays",
                table: "installation_settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.AddColumn<int>(
                name: "GameHistoryDays",
                table: "cloud_vaults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 90);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityRetentionDays",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "GameHistoryDays",
                table: "cloud_vaults");
        }
    }
}
