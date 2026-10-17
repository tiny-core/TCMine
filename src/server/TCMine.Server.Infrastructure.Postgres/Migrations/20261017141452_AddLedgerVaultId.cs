using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerVaultId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VaultId",
                table: "cloud_ledger",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_cloud_ledger_VaultId_Id",
                table: "cloud_ledger",
                columns: new[] { "VaultId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cloud_ledger_VaultId_Id",
                table: "cloud_ledger");

            migrationBuilder.DropColumn(
                name: "VaultId",
                table: "cloud_ledger");
        }
    }
}
