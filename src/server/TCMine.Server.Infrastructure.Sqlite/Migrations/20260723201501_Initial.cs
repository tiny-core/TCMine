using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blobs",
                columns: table => new
                {
                    Sha256 = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastAccessedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blobs", x => x.Sha256);
                });

            migrationBuilder.CreateTable(
                name: "game_servers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ModpackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModpackVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectAddress = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ContainerId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    MemoryMb = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxPlayers = table.Column<int>(type: "INTEGER", nullable: false),
                    RconSecret = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_servers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "modpacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    IconBlobSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modpacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MicrosoftObjectId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MinecraftUuid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsInstanceAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "modpack_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModpackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    MinecraftVersion = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Loader = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LoaderVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RecommendedMemoryMb = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modpack_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_modpack_versions_modpacks_ModpackId",
                        column: x => x.ModpackId,
                        principalTable: "modpacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "modpack_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModpackVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Optional = table.Column<bool>(type: "INTEGER", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OriginReference = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modpack_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_modpack_files_modpack_versions_ModpackVersionId",
                        column: x => x.ModpackVersionId,
                        principalTable: "modpack_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_servers_ModpackVersionId",
                table: "game_servers",
                column: "ModpackVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_game_servers_OwnerId",
                table: "game_servers",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_GameServerId",
                table: "memberships",
                column: "GameServerId");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_UserId_GameServerId",
                table: "memberships",
                columns: new[] { "UserId", "GameServerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_modpack_files_ModpackVersionId_Path",
                table: "modpack_files",
                columns: new[] { "ModpackVersionId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_modpack_files_Sha256",
                table: "modpack_files",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_modpack_versions_ModpackId_Version",
                table: "modpack_versions",
                columns: new[] { "ModpackId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_modpacks_OwnerId",
                table: "modpacks",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_modpacks_Slug",
                table: "modpacks",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_MicrosoftObjectId",
                table: "users",
                column: "MicrosoftObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_MinecraftUuid",
                table: "users",
                column: "MinecraftUuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blobs");

            migrationBuilder.DropTable(
                name: "game_servers");

            migrationBuilder.DropTable(
                name: "memberships");

            migrationBuilder.DropTable(
                name: "modpack_files");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "modpack_versions");

            migrationBuilder.DropTable(
                name: "modpacks");
        }
    }
}
