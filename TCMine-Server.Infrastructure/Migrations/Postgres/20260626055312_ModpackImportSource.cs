#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace TCMine_Server.Infrastructure.Migrations.Postgres;

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
                ModpackId = table.Column<Guid>("uuid", nullable: false),
                CurseProjectId = table.Column<long>("bigint", nullable: false),
                CurseProjectName = table.Column<string>("character varying(200)", maxLength: 200, nullable: false),
                InstalledFileId = table.Column<long>("bigint", nullable: false),
                InstalledVersion = table.Column<string>("character varying(120)", maxLength: 120, nullable: true),
                ImportedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                LastCheckedAt = table.Column<DateTime>("timestamp with time zone", nullable: true),
                LatestFileId = table.Column<long>("bigint", nullable: true),
                LatestVersion = table.Column<string>("character varying(120)", maxLength: 120, nullable: true)
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