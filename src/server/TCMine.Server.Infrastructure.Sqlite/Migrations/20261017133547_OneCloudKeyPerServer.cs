using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class OneCloudKeyPerServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cloud_server_credentials_GameServerId",
                table: "cloud_server_credentials");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReissuedAt",
                table: "cloud_server_credentials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cloud_server_credentials_GameServerId",
                table: "cloud_server_credentials",
                column: "GameServerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cloud_server_credentials_GameServerId",
                table: "cloud_server_credentials");

            migrationBuilder.DropColumn(
                name: "ReissuedAt",
                table: "cloud_server_credentials");

            migrationBuilder.CreateIndex(
                name: "IX_cloud_server_credentials_GameServerId",
                table: "cloud_server_credentials",
                column: "GameServerId");
        }
    }
}
