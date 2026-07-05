#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace TCMine_Server.Infrastructure.Migrations.Sqlite;

/// <inheritdoc />
public partial class ServerInstanceRuntimeAndCache : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "Pid",
            "ServerInstances");

        migrationBuilder.AddColumn<string>(
            "ContainerId",
            "ServerInstances",
            "TEXT",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            "ExtraJvmArgs",
            "ServerInstances",
            "TEXT",
            maxLength: 2000,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            "ImageTag",
            "ServerInstances",
            "TEXT",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            "ProvisionedAt",
            "ServerInstances",
            "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            "XmsMb",
            "ServerInstances",
            "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            "ServerRuntimeCache",
            table => new
            {
                Id = table.Column<Guid>("TEXT", nullable: false),
                Loader = table.Column<string>("TEXT", maxLength: 20, nullable: false),
                LoaderVersion = table.Column<string>("TEXT", maxLength: 40, nullable: false),
                MinecraftVersion = table.Column<string>("TEXT", maxLength: 40, nullable: false),
                RelativePath = table.Column<string>("TEXT", maxLength: 200, nullable: false),
                SizeBytes = table.Column<long>("INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>("TEXT", nullable: false),
                LastUsedAt = table.Column<DateTime>("TEXT", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_ServerRuntimeCache", x => x.Id); });

        migrationBuilder.CreateIndex(
            "IX_ServerRuntimeCache_Loader_LoaderVersion_MinecraftVersion",
            "ServerRuntimeCache",
            new[] { "Loader", "LoaderVersion", "MinecraftVersion" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "ServerRuntimeCache");

        migrationBuilder.DropColumn(
            "ContainerId",
            "ServerInstances");

        migrationBuilder.DropColumn(
            "ExtraJvmArgs",
            "ServerInstances");

        migrationBuilder.DropColumn(
            "ImageTag",
            "ServerInstances");

        migrationBuilder.DropColumn(
            "ProvisionedAt",
            "ServerInstances");

        migrationBuilder.DropColumn(
            "XmsMb",
            "ServerInstances");

        migrationBuilder.AddColumn<int>(
            "Pid",
            "ServerInstances",
            "INTEGER",
            nullable: true);
    }
}