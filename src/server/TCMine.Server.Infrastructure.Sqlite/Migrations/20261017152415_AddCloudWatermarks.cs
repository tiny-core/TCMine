using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudWatermarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cloud_watermarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VaultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerUuid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Epoch = table.Column<long>(type: "INTEGER", nullable: false),
                    Seq = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_watermarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_watermarks_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cloud_watermarks_game_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "game_servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_watermarks_ServerId",
                table: "cloud_watermarks",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_cloud_watermarks_VaultId_ServerId_PlayerUuid",
                table: "cloud_watermarks",
                columns: new[] { "VaultId", "ServerId", "PlayerUuid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cloud_watermarks");
        }
    }
}
