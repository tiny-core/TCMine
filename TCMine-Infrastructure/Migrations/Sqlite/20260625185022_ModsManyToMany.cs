using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ModsManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mods");

            migrationBuilder.CreateTable(
                name: "ModFiles",
                columns: table => new
                {
                    FileId = table.Column<long>(type: "INTEGER", nullable: false),
                    CurseModId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    DownloadUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Sha1 = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    FileLength = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModFiles", x => x.FileId);
                });

            migrationBuilder.CreateTable(
                name: "ModpackMods",
                columns: table => new
                {
                    ModpackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileId = table.Column<long>(type: "INTEGER", nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModpackMods", x => new { x.ModpackId, x.FileId });
                    table.ForeignKey(
                        name: "FK_ModpackMods_ModFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "ModFiles",
                        principalColumn: "FileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModpackMods_Modpacks_ModpackId",
                        column: x => x.ModpackId,
                        principalTable: "Modpacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModpackMods_FileId",
                table: "ModpackMods",
                column: "FileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModpackMods");

            migrationBuilder.DropTable(
                name: "ModFiles");

            migrationBuilder.CreateTable(
                name: "Mods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModpackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurseModId = table.Column<long>(type: "INTEGER", nullable: false),
                    DownloadUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileId = table.Column<long>(type: "INTEGER", nullable: false),
                    FileLength = table.Column<long>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Sha1 = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Side = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mods_Modpacks_ModpackId",
                        column: x => x.ModpackId,
                        principalTable: "Modpacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mods_ModpackId",
                table: "Mods",
                column: "ModpackId");
        }
    }
}
