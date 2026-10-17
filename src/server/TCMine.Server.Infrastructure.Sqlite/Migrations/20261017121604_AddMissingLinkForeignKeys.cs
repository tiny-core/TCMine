using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingLinkForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_invites_game_servers_GameServerId",
                table: "invites",
                column: "GameServerId",
                principalTable: "game_servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_memberships_game_servers_GameServerId",
                table: "memberships",
                column: "GameServerId",
                principalTable: "game_servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_modpack_memberships_modpacks_ModpackId",
                table: "modpack_memberships",
                column: "ModpackId",
                principalTable: "modpacks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_news_modpacks_ModpackId",
                table: "news",
                column: "ModpackId",
                principalTable: "modpacks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invites_game_servers_GameServerId",
                table: "invites");

            migrationBuilder.DropForeignKey(
                name: "FK_memberships_game_servers_GameServerId",
                table: "memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_modpack_memberships_modpacks_ModpackId",
                table: "modpack_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_news_modpacks_ModpackId",
                table: "news");
        }
    }
}
