using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Server.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class DropPlayerConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerConfigs",
                columns: table => new
                {
                    Uuid = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ModpackId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerConfigs", x => new { x.Uuid, x.ModpackId });
                });
        }
    }
}
