using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Server.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ModpackImportSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModpackImportSources",
                columns: table => new
                {
                    ModpackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurseProjectId = table.Column<long>(type: "INTEGER", nullable: false),
                    CurseProjectName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    InstalledFileId = table.Column<long>(type: "INTEGER", nullable: false),
                    InstalledVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LatestFileId = table.Column<long>(type: "INTEGER", nullable: true),
                    LatestVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModpackImportSources", x => x.ModpackId);
                    table.ForeignKey(
                        name: "FK_ModpackImportSources_Modpacks_ModpackId",
                        column: x => x.ModpackId,
                        principalTable: "Modpacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModpackImportSources");
        }
    }
}
