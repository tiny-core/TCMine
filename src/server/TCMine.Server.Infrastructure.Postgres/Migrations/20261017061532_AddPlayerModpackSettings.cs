using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerModpackSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_modpack_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModpackId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyBindings = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Options = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_modpack_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_modpack_settings_modpacks_ModpackId",
                        column: x => x.ModpackId,
                        principalTable: "modpacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_player_modpack_settings_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_modpack_settings_ModpackId",
                table: "player_modpack_settings",
                column: "ModpackId");

            migrationBuilder.CreateIndex(
                name: "IX_player_modpack_settings_UserId_ModpackId",
                table: "player_modpack_settings",
                columns: new[] { "UserId", "ModpackId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_modpack_settings");
        }
    }
}
