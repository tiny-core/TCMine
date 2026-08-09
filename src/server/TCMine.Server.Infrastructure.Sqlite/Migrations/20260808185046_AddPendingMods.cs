using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingMods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_mods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModpackVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectSlug = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FileId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    PageUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Side = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_mods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pending_mods_modpack_versions_ModpackVersionId",
                        column: x => x.ModpackVersionId,
                        principalTable: "modpack_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pending_mods_ModpackVersionId_ProjectSlug",
                table: "pending_mods",
                columns: new[] { "ModpackVersionId", "ProjectSlug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_mods");
        }
    }
}
