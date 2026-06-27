using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ServerInstanceRuntimeAndCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pid",
                table: "ServerInstances");

            migrationBuilder.AddColumn<string>(
                name: "ContainerId",
                table: "ServerInstances",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtraJvmArgs",
                table: "ServerInstances",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImageTag",
                table: "ServerInstances",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProvisionedAt",
                table: "ServerInstances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "XmsMb",
                table: "ServerInstances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ServerRuntimeCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Loader = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LoaderVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MinecraftVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerRuntimeCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServerRuntimeCache_Loader_LoaderVersion_MinecraftVersion",
                table: "ServerRuntimeCache",
                columns: new[] { "Loader", "LoaderVersion", "MinecraftVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerRuntimeCache");

            migrationBuilder.DropColumn(
                name: "ContainerId",
                table: "ServerInstances");

            migrationBuilder.DropColumn(
                name: "ExtraJvmArgs",
                table: "ServerInstances");

            migrationBuilder.DropColumn(
                name: "ImageTag",
                table: "ServerInstances");

            migrationBuilder.DropColumn(
                name: "ProvisionedAt",
                table: "ServerInstances");

            migrationBuilder.DropColumn(
                name: "XmsMb",
                table: "ServerInstances");

            migrationBuilder.AddColumn<int>(
                name: "Pid",
                table: "ServerInstances",
                type: "integer",
                nullable: true);
        }
    }
}
