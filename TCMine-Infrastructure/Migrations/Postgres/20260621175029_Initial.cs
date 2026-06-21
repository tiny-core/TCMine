using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TCMine_Infrastructure.Migrations.Postgres;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "Modpacks",
            table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Name = table.Column<string>("character varying(120)", maxLength: 120, nullable: false),
                Version = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                Minecraft = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                Loader = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                LoaderVersion = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                Description = table.Column<string>("character varying(2000)", maxLength: 2000, nullable: false),
                IsPublished = table.Column<bool>("boolean", nullable: false),
                RecommendedRamMb = table.Column<int>("integer", nullable: true),
                HasOverrides = table.Column<bool>("boolean", nullable: false),
                UpdatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Modpacks", x => x.Id); });

        migrationBuilder.CreateTable(
            "News",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Tag = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                Title = table.Column<string>("character varying(200)", maxLength: 200, nullable: false),
                Summary = table.Column<string>("character varying(1000)", maxLength: 1000, nullable: false),
                PublishedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                IsPublished = table.Column<bool>("boolean", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_News", x => x.Id); });

        migrationBuilder.CreateTable(
            "OverrideHistory",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ModpackId = table.Column<Guid>("uuid", nullable: false),
                Operation = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                PathBefore = table.Column<string>("character varying(400)", maxLength: 400, nullable: true),
                PathAfter = table.Column<string>("character varying(400)", maxLength: 400, nullable: true),
                ContentBefore = table.Column<string>("text", nullable: true),
                Actor = table.Column<string>("character varying(80)", maxLength: 80, nullable: true),
                CreatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_OverrideHistory", x => x.Id); });

        migrationBuilder.CreateTable(
            "PlayerConfigs",
            table => new
            {
                Uuid = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                ModpackId = table.Column<string>("character varying(80)", maxLength: 80, nullable: false),
                UpdatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_PlayerConfigs", x => new { x.Uuid, x.ModpackId }); });

        migrationBuilder.CreateTable(
            "Releases",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Version = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                Channel = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Notes = table.Column<string>("character varying(4000)", maxLength: 4000, nullable: false),
                PublishedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                Files = table.Column<string>("character varying(4000)", maxLength: 4000, nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Releases", x => x.Id); });

        migrationBuilder.CreateTable(
            "Settings",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false),
                CfApiKeyEncrypted = table.Column<string>("character varying(256)", maxLength: 256, nullable: true),
                AzureClientId = table.Column<string>("character varying(64)", maxLength: 64, nullable: true),
                AzureTenantId = table.Column<string>("character varying(64)", maxLength: 64, nullable: true),
                PublicBaseUrl = table.Column<string>("character varying(256)", maxLength: 256, nullable: true),
                UpdatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Settings", x => x.Id); });

        migrationBuilder.CreateTable(
            "Users",
            table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Username = table.Column<string>("character varying(60)", maxLength: 60, nullable: false),
                PasswordHash = table.Column<string>("character varying(400)", maxLength: 400, nullable: false),
                Role = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                IsActive = table.Column<bool>("boolean", nullable: false),
                CreatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                LastLoginAt = table.Column<DateTime>("timestamp with time zone", nullable: true)
            },
            constraints: table => { table.PrimaryKey("PK_Users", x => x.Id); });

        migrationBuilder.CreateTable(
            "Mods",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CurseModId = table.Column<long>("bigint", nullable: false),
                FileId = table.Column<long>("bigint", nullable: false),
                Name = table.Column<string>("character varying(200)", maxLength: 200, nullable: false),
                Version = table.Column<string>("character varying(80)", maxLength: 80, nullable: true),
                FileName = table.Column<string>("character varying(260)", maxLength: 260, nullable: false),
                DownloadUrl = table.Column<string>("character varying(500)", maxLength: 500, nullable: false),
                Sha1 = table.Column<string>("character varying(40)", maxLength: 40, nullable: true),
                FileLength = table.Column<long>("bigint", nullable: false),
                Target = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Side = table.Column<string>("character varying(10)", maxLength: 10, nullable: false),
                ModpackId = table.Column<Guid>("uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Mods", x => x.Id);
                table.ForeignKey(
                    "FK_Mods_Modpacks_ModpackId",
                    x => x.ModpackId,
                    "Modpacks",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "ServerInstances",
            table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Name = table.Column<string>("character varying(120)", maxLength: 120, nullable: false),
                ModpackId = table.Column<Guid>("uuid", nullable: false),
                Port = table.Column<int>("integer", nullable: false),
                RamMb = table.Column<int>("integer", nullable: false),
                MaxPlayers = table.Column<int>("integer", nullable: false),
                Motd = table.Column<string>("character varying(120)", maxLength: 120, nullable: false),
                Directory = table.Column<string>("character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Pid = table.Column<int>("integer", nullable: true),
                AutoRestart = table.Column<bool>("boolean", nullable: false),
                CreatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServerInstances", x => x.Id);
                table.ForeignKey(
                    "FK_ServerInstances_Modpacks_ModpackId",
                    x => x.ModpackId,
                    "Modpacks",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            "Servers",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>("character varying(120)", maxLength: 120, nullable: false),
                Address = table.Column<string>("character varying(200)", maxLength: 200, nullable: false),
                Port = table.Column<int>("integer", nullable: false),
                ModpackId = table.Column<Guid>("uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Servers", x => x.Id);
                table.ForeignKey(
                    "FK_Servers_Modpacks_ModpackId",
                    x => x.ModpackId,
                    "Modpacks",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_Mods_ModpackId",
            "Mods",
            "ModpackId");

        migrationBuilder.CreateIndex(
            "IX_OverrideHistory_ModpackId_CreatedAt",
            "OverrideHistory",
            new[] { "ModpackId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            "IX_ServerInstances_ModpackId",
            "ServerInstances",
            "ModpackId");

        migrationBuilder.CreateIndex(
            "IX_Servers_ModpackId",
            "Servers",
            "ModpackId");

        migrationBuilder.CreateIndex(
            "IX_Users_Username",
            "Users",
            "Username",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "Mods");

        migrationBuilder.DropTable(
            "News");

        migrationBuilder.DropTable(
            "OverrideHistory");

        migrationBuilder.DropTable(
            "PlayerConfigs");

        migrationBuilder.DropTable(
            "Releases");

        migrationBuilder.DropTable(
            "ServerInstances");

        migrationBuilder.DropTable(
            "Servers");

        migrationBuilder.DropTable(
            "Settings");

        migrationBuilder.DropTable(
            "Users");

        migrationBuilder.DropTable(
            "Modpacks");
    }
}