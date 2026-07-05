#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace TCMine_Server.Infrastructure.Migrations.Postgres;

/// <inheritdoc />
public partial class ServerInstanceAdvertise : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            "ServerInstanceId",
            "Servers",
            "uuid",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            "Advertise",
            "ServerInstances",
            "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            "PublicAddress",
            "ServerInstances",
            "character varying(200)",
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