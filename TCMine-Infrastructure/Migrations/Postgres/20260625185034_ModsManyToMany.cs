using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TCMine_Infrastructure.Migrations.Postgres;

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
                FileId = table.Column<long>("bigint", nullable: false),
                CurseModId = table.Column<long>("bigint", nullable: false),
                Name = table.Column<string>("character varying(200)", maxLength: 200, nullable: false),
                Version = table.Column<string>("character varying(80)", maxLength: 80, nullable: true),
                FileName = table.Column<string>("character varying(260)", maxLength: 260, nullable: false),
                DownloadUrl = table.Column<string>("character varying(500)", maxLength: 500, nullable: false),
                Sha1 = table.Column<string>("character varying(40)", maxLength: 40, nullable: true),
                FileLength = table.Column<long>("bigint", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_ModFiles", x => x.FileId); });

        migrationBuilder.CreateTable(
            "ModpackMods",
            table => new
            {
                ModpackId = table.Column<Guid>("uuid", nullable: false),
                FileId = table.Column<long>("bigint", nullable: false),
                Target = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Side = table.Column<string>("character varying(10)", maxLength: 10, nullable: false),
                SortOrder = table.Column<int>("integer", nullable: false)
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
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ModpackId = table.Column<Guid>("uuid", nullable: false),
                CurseModId = table.Column<long>("bigint", nullable: false),
                DownloadUrl = table.Column<string>("character varying(500)", maxLength: 500, nullable: false),
                FileId = table.Column<long>("bigint", nullable: false),
                FileLength = table.Column<long>("bigint", nullable: false),
                FileName = table.Column<string>("character varying(260)", maxLength: 260, nullable: false),
                Name = table.Column<string>("character varying(200)", maxLength: 200, nullable: false),
                Sha1 = table.Column<string>("character varying(40)", maxLength: 40, nullable: true),
                Side = table.Column<string>("character varying(10)", maxLength: 10, nullable: false),
                Target = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Version = table.Column<string>("character varying(80)", maxLength: 80, nullable: true)
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