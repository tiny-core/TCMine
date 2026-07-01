using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Server.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ServerInstanceAdvertise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServerInstanceId",
                table: "Servers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Advertise",
                table: "ServerInstances",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicAddress",
                table: "ServerInstances",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServerInstanceId",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "Advertise",
                table: "ServerInstances");

            migrationBuilder.DropColumn(
                name: "PublicAddress",
                table: "ServerInstances");
        }
    }
}
