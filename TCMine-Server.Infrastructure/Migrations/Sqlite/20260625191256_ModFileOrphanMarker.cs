#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace TCMine_Server.Infrastructure.Migrations.Sqlite;

/// <inheritdoc />
public partial class ModFileOrphanMarker : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            "OrphanedAt",
            "ModFiles",
            "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "OrphanedAt",
            "ModFiles");
    }
}