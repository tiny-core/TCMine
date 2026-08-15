using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldBackups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "world_backups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ModpackVersionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModpackVersionLabel = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_backups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_world_backups_game_servers_GameServerId",
                        column: x => x.GameServerId,
                        principalTable: "game_servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_world_backups_GameServerId",
                table: "world_backups",
                column: "GameServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "world_backups");
        }
    }
}
