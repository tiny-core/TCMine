using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Infrastructure.Migrations.Sqlite
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
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtraJvmArgs",
                table: "ServerInstances",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImageTag",
                table: "ServerInstances",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProvisionedAt",
                table: "ServerInstances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "XmsMb",
                table: "ServerInstances",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ServerRuntimeCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Loader = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LoaderVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    MinecraftVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                type: "INTEGER",
                nullable: true);
        }
    }
}
