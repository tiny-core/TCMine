using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddModpackFileDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequiredDependencies",
                table: "modpack_files",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_modpack_files_Origin_OriginReference",
                table: "modpack_files",
                columns: new[] { "Origin", "OriginReference" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_modpack_files_Origin_OriginReference",
                table: "modpack_files");

            migrationBuilder.DropColumn(
                name: "RequiredDependencies",
                table: "modpack_files");
        }
    }
}
