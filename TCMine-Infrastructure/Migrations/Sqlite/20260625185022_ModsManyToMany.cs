using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Infrastructure.Migrations.Sqlite;

/// <inheritdoc />
public partial class ModsManyToMany : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "Mods");

        migrationBuilder.CreateTable(
            "ModFiles",
            table => new
            {
                FileId = table.Column<long>("INTEGER", nullable: false),
                CurseModId = table.Column<long>("INTEGER", nullable: false),
                Name = table.Column<string>("TEXT", maxLength: 200, nullable: false),
                Version = table.Column<string>("TEXT", maxLength: 80, nullable: true),
                FileName = table.Column<string>("TEXT", maxLength: 260, nullable: false),
                DownloadUrl = table.Column<string>("TEXT", maxLength: 500, nullable: false),
                Sha1 = table.Column<string>("TEXT", maxLength: 40, nullable: true),
                FileLength = table.Column<long>("INTEGER", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_ModFiles", x => x.FileId); });

        migrationBuilder.CreateTable(
            "ModpackMods",
            table => new
            {
                ModpackId = table.Column<Guid>("TEXT", nullable: false),
                FileId = table.Column<long>("INTEGER", nullable: false),
                Target = table.Column<string>("TEXT", maxLength: 20, nullable: false),
                Side = table.Column<string>("TEXT", maxLength: 10, nullable: false),
                SortOrder = table.Column<int>("INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModpackMods", x => new { x.ModpackId, x.FileId });
                table.ForeignKey(
                    "FK_ModpackMods_ModFiles_FileId",
                    x => x.FileId,
                    "ModFiles",
                    "FileId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_ModpackMods_Modpacks_ModpackId",
                    x => x.ModpackId,
                    "Modpacks",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_ModpackMods_FileId",
            "ModpackMods",
            "FileId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "ModpackMods");

        migrationBuilder.DropTable(
            "ModFiles");

        migrationBuilder.CreateTable(
            "Mods",
            table => new
            {
                Id = table.Column<int>("INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ModpackId = table.Column<Guid>("TEXT", nullable: false),
                CurseModId = table.Column<long>("INTEGER", nullable: false),
                DownloadUrl = table.Column<string>("TEXT", maxLength: 500, nullable: false),
                FileId = table.Column<long>("INTEGER", nullable: false),
                FileLength = table.Column<long>("INTEGER", nullable: false),
                FileName = table.Column<string>("TEXT", maxLength: 260, nullable: false),
                Name = table.Column<string>("TEXT", maxLength: 200, nullable: false),
                Sha1 = table.Column<string>("TEXT", maxLength: 40, nullable: true),
                Side = table.Column<string>("TEXT", maxLength: 10, nullable: false),
                Target = table.Column<string>("TEXT", maxLength: 20, nullable: false),
                Version = table.Column<string>("TEXT", maxLength: 80, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Mods", x => x.Id);
                table.ForeignKey(
                    "FK_Mods_Modpacks_ModpackId",
                    x => x.ModpackId,
                    "Modpacks",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_Mods_ModpackId",
            "Mods",
            "ModpackId");
    }
}