#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace TCMine_Server.Infrastructure.Migrations.Sqlite;

/// <inheritdoc />
public partial class DropPlayerConfigs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "PlayerConfigs");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "PlayerConfigs",
            table => new
            {
                Uuid = table.Column<string>("TEXT", maxLength: 40, nullable: false),
                ModpackId = table.Column<string>("TEXT", maxLength: 80, nullable: false),
                UpdatedAt = table.Column<DateTime>("TEXT", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_PlayerConfigs", x => new { x.Uuid, x.ModpackId }); });
    }
}