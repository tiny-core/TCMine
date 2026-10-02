using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CloudVaultId",
                table: "game_servers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cloud_item_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ModId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Encoded = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_item_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cloud_vaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PolicyVersion = table.Column<long>(type: "bigint", nullable: false),
                    LeaseTtlMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxItemBytes = table.Column<int>(type: "integer", nullable: false),
                    MaxChannelsPerPlayer = table.Column<int>(type: "integer", nullable: false),
                    MaxTypesPerChannel = table.Column<int>(type: "integer", nullable: false),
                    MaxTotalPerChannel = table.Column<long>(type: "bigint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_vaults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cloud_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VaultId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerUuid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Epoch = table.Column<long>(type: "bigint", nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_batches_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cloud_channels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VaultId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerUuid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FrozenReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_channels_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cloud_leases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VaultId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerUuid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HolderServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Epoch = table.Column<long>(type: "bigint", nullable: false),
                    LastSeq = table.Column<long>(type: "bigint", nullable: false),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_leases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_leases_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cloud_server_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VaultId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_server_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_server_credentials_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cloud_server_credentials_game_servers_GameServerId",
                        column: x => x.GameServerId,
                        principalTable: "game_servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cloud_quarantine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VaultId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_quarantine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_quarantine_cloud_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "cloud_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cloud_quarantine_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cloud_balances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_balances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_balances_cloud_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "cloud_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cloud_balances_cloud_item_types_ItemTypeId",
                        column: x => x.ItemTypeId,
                        principalTable: "cloud_item_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cloud_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Delta = table.Column<long>(type: "bigint", nullable: false),
                    BalanceAfter = table.Column<long>(type: "bigint", nullable: false),
                    Source = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_ledger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_ledger_cloud_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "cloud_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cloud_ledger_cloud_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "cloud_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cloud_ledger_cloud_item_types_ItemTypeId",
                        column: x => x.ItemTypeId,
                        principalTable: "cloud_item_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_servers_CloudVaultId",
                table: "game_servers",
                column: "CloudVaultId");

            migrationBuilder.CreateIndex(
                name: "IX_cloud_balances_ChannelId_ItemTypeId",
                table: "cloud_balances",
                columns: new[] { "ChannelId", "ItemTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cloud_balances_ItemTypeId",
                table: "cloud_balances",
                column: "ItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_cloud_batches_ServerId_Epoch_Seq",
                table: "cloud_batches",
                columns: new[] { "ServerId", "Epoch", "Seq" });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_batches_VaultId_PlayerUuid_Epoch_Seq",
                table: "cloud_batches",
                columns: new[] { "VaultId", "PlayerUuid", "Epoch", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cloud_channels_VaultId_PlayerUuid_Name",
                table: "cloud_channels",
                columns: new[] { "VaultId", "PlayerUuid", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cloud_item_types_Fingerprint",
                table: "cloud_item_types",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cloud_leases_HolderServerId",
                table: "cloud_leases",
                column: "HolderServerId");

            migrationBuilder.CreateIndex(
                name: "IX_cloud_leases_VaultId_PlayerUuid",
                table: "cloud_leases",
                columns: new[] { "VaultId", "PlayerUuid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cloud_ledger_BatchId",
                table: "cloud_ledger",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_cloud_ledger_ChannelId_Id",
                table: "cloud_ledger",
                columns: new[] { "ChannelId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_ledger_ItemTypeId",
                table: "cloud_ledger",
                column: "ItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_cloud_quarantine_BatchId",
                table: "cloud_quarantine",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_cloud_quarantine_VaultId_ResolvedAt",
                table: "cloud_quarantine",
                columns: new[] { "VaultId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_server_credentials_GameServerId",
                table: "cloud_server_credentials",
                column: "GameServerId");

            migrationBuilder.CreateIndex(
                name: "IX_cloud_server_credentials_KeyPrefix",
                table: "cloud_server_credentials",
                column: "KeyPrefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cloud_server_credentials_VaultId",
                table: "cloud_server_credentials",
                column: "VaultId");

            migrationBuilder.CreateIndex(
                name: "IX_cloud_vaults_OwnerId",
                table: "cloud_vaults",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_game_servers_cloud_vaults_CloudVaultId",
                table: "game_servers",
                column: "CloudVaultId",
                principalTable: "cloud_vaults",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_game_servers_cloud_vaults_CloudVaultId",
                table: "game_servers");

            migrationBuilder.DropTable(
                name: "cloud_balances");

            migrationBuilder.DropTable(
                name: "cloud_leases");

            migrationBuilder.DropTable(
                name: "cloud_ledger");

            migrationBuilder.DropTable(
                name: "cloud_quarantine");

            migrationBuilder.DropTable(
                name: "cloud_server_credentials");

            migrationBuilder.DropTable(
                name: "cloud_channels");

            migrationBuilder.DropTable(
                name: "cloud_item_types");

            migrationBuilder.DropTable(
                name: "cloud_batches");

            migrationBuilder.DropTable(
                name: "cloud_vaults");

            migrationBuilder.DropIndex(
                name: "IX_game_servers_CloudVaultId",
                table: "game_servers");

            migrationBuilder.DropColumn(
                name: "CloudVaultId",
                table: "game_servers");
        }
    }
}
