#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace TCMine_Server.Infrastructure.Migrations.Sqlite;

/// <inheritdoc />
public partial class ServerInstanceAdvertise : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            "ServerInstanceId",
            "Servers",
            "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            "Advertise",
            "ServerInstances",
            "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            "PublicAddress",
            "ServerInstances",
            "TEXT",
            maxLength: 200,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "ServerInstanceId",
            "Servers");

        migrationBuilder.DropColumn(
            "Advertise",
            "ServerInstances");

        migrationBuilder.DropColumn(
            "PublicAddress",
            "ServerInstances");
    }
}