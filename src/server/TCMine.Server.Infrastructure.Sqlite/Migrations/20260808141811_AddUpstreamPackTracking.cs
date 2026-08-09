using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddUpstreamPackTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UpstreamProjectId",
                table: "modpacks",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpstreamProvider",
                table: "modpacks",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpstreamFileId",
                table: "modpack_versions",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpstreamSnapshotJson",
                table: "modpack_versions",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpstreamVersionLabel",
                table: "modpack_versions",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_modpacks_UpstreamProvider_UpstreamProjectId",
                table: "modpacks",
                columns: new[] { "UpstreamProvider", "UpstreamProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_modpacks_UpstreamProvider_UpstreamProjectId",
                table: "modpacks");

            migrationBuilder.DropColumn(
                name: "UpstreamProjectId",
                table: "modpacks");

            migrationBuilder.DropColumn(
                name: "UpstreamProvider",
                table: "modpacks");

            migrationBuilder.DropColumn(
                name: "UpstreamFileId",
                table: "modpack_versions");

            migrationBuilder.DropColumn(
                name: "UpstreamSnapshotJson",
                table: "modpack_versions");

            migrationBuilder.DropColumn(
                name: "UpstreamVersionLabel",
                table: "modpack_versions");
        }
    }
}
