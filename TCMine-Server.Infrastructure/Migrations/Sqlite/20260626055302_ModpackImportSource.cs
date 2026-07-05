#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace TCMine_Server.Infrastructure.Migrations.Sqlite;

/// <inheritdoc />
public partial class ModpackImportSource : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "ModpackImportSources",
            table => new
            {
                ModpackId = table.Column<Guid>("TEXT", nullable: false),
                CurseProjectId = table.Column<long>("INTEGER", nullable: false),
                CurseProjectName = table.Column<string>("TEXT", maxLength: 200, nullable: false),
                InstalledFileId = table.Column<long>("INTEGER", nullable: false),
                InstalledVersion = table.Column<string>("TEXT", maxLength: 120, nullable: true),
                ImportedAt = table.Column<DateTime>("TEXT", nullable: false),
                LastCheckedAt = table.Column<DateTime>("TEXT", nullable: true),
                LatestFileId = table.Column<long>("INTEGER", nullable: true),
                LatestVersion = table.Column<string>("TEXT", maxLength: 120, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModpackImportSources", x => x.ModpackId);
                table.ForeignKey(
                    "FK_ModpackImportSources_Modpacks_ModpackId",
                    x => x.ModpackId,
                    "Modpacks",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "ModpackImportSources");
    }
}