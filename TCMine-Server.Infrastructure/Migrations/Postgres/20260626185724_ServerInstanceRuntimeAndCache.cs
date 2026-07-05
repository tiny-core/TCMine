#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace TCMine_Server.Infrastructure.Migrations.Postgres;

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
            "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            "ExtraJvmArgs",
            "ServerInstances",
            "character varying(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            "ImageTag",
            "ServerInstances",
            "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            "ProvisionedAt",
            "ServerInstances",
            "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            "XmsMb",
            "ServerInstances",
            "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            "ServerRuntimeCache",
            table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Loader = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                LoaderVersion = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                MinecraftVersion = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                RelativePath = table.Column<string>("character varying(200)", maxLength: 200, nullable: false),
                SizeBytes = table.Column<long>("bigint", nullable: false),
                CreatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                LastUsedAt = table.Column<DateTime>("timestamp with time zone", nullable: false)
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
            "integer",
            nullable: true);
    }
}