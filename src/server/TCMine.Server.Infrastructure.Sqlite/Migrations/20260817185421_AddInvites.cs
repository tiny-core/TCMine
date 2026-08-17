using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CodeHash = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    GameServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RedeemedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invites_CodeHash",
                table: "invites",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invites_GameServerId",
                table: "invites",
                column: "GameServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invites");
        }
    }
}
