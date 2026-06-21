using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Modpacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Minecraft = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Loader = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LoaderVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecommendedRamMb = table.Column<int>(type: "INTEGER", nullable: true),
                    HasOverrides = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modpacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "News",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tag = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OverrideHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModpackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PathBefore = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    PathAfter = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    ContentBefore = table.Column<string>(type: "TEXT", nullable: true),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverrideHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerConfigs",
                columns: table => new
                {
                    Uuid = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ModpackId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerConfigs", x => new { x.Uuid, x.ModpackId });
                });

            migrationBuilder.CreateTable(
                name: "Releases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Version = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Files = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Releases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    CfApiKeyEncrypted = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AzureClientId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AzureTenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PublicBaseUrl = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CurseModId = table.Column<long>(type: "INTEGER", nullable: false),
                    FileId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    DownloadUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Sha1 = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    FileLength = table.Column<long>(type: "INTEGER", nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ModpackId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mods_Modpacks_ModpackId",
                        column: x => x.ModpackId,
                        principalTable: "Modpacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServerInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ModpackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    RamMb = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxPlayers = table.Column<int>(type: "INTEGER", nullable: false),
                    Motd = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Directory = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Pid = table.Column<int>(type: "INTEGER", nullable: true),
                    AutoRestart = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerInstances_Modpacks_ModpackId",
                        column: x => x.ModpackId,
                        principalTable: "Modpacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    ModpackId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Servers_Modpacks_ModpackId",
                        column: x => x.ModpackId,
                        principalTable: "Modpacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mods_ModpackId",
                table: "Mods",
                column: "ModpackId");

            migrationBuilder.CreateIndex(
                name: "IX_OverrideHistory_ModpackId_CreatedAt",
                table: "OverrideHistory",
                columns: new[] { "ModpackId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServerInstances_ModpackId",
                table: "ServerInstances",
                column: "ModpackId");

            migrationBuilder.CreateIndex(
                name: "IX_Servers_ModpackId",
                table: "Servers",
                column: "ModpackId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mods");

            migrationBuilder.DropTable(
                name: "News");

            migrationBuilder.DropTable(
                name: "OverrideHistory");

            migrationBuilder.DropTable(
                name: "PlayerConfigs");

            migrationBuilder.DropTable(
                name: "Releases");

            migrationBuilder.DropTable(
                name: "ServerInstances");

            migrationBuilder.DropTable(
                name: "Servers");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Modpacks");
        }
    }
}
