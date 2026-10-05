using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cloud_admin_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VaultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_admin_audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_admin_audit_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cloud_doubtful_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VaultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReportId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerUuid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ChannelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_doubtful_operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_doubtful_operations_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cloud_item_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VaultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_item_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_item_rules_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cloud_rollback_incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VaultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorldId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CheckpointJson = table.Column<string>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_rollback_incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_rollback_incidents_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cloud_suspect_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VaultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Evidence = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Attempts = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_suspect_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cloud_suspect_items_cloud_vaults_VaultId",
                        column: x => x.VaultId,
                        principalTable: "cloud_vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_admin_audit_VaultId_Id",
                table: "cloud_admin_audit",
                columns: new[] { "VaultId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_doubtful_operations_ServerId_ReportId_Index",
                table: "cloud_doubtful_operations",
                columns: new[] { "ServerId", "ReportId", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cloud_doubtful_operations_VaultId_Status",
                table: "cloud_doubtful_operations",
                columns: new[] { "VaultId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_item_rules_VaultId_Scope_Pattern",
                table: "cloud_item_rules",
                columns: new[] { "VaultId", "Scope", "Pattern" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cloud_rollback_incidents_ServerId_Status",
                table: "cloud_rollback_incidents",
                columns: new[] { "ServerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_rollback_incidents_VaultId_Status",
                table: "cloud_rollback_incidents",
                columns: new[] { "VaultId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_suspect_items_VaultId_ItemId",
                table: "cloud_suspect_items",
                columns: new[] { "VaultId", "ItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cloud_admin_audit");

            migrationBuilder.DropTable(
                name: "cloud_doubtful_operations");

            migrationBuilder.DropTable(
                name: "cloud_item_rules");

            migrationBuilder.DropTable(
                name: "cloud_rollback_incidents");

            migrationBuilder.DropTable(
                name: "cloud_suspect_items");
        }
    }
}
