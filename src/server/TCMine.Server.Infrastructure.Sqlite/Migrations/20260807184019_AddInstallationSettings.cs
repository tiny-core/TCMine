using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "installation_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DefaultMinecraftVersion = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    DefaultLoader = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DefaultMemoryMb = table.Column<int>(type: "INTEGER", nullable: false),
                    CurseForgeApiKeyEncrypted = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    SmtpHost = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpUser = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SmtpPasswordEncrypted = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    SmtpFrom = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SmtpUseTls = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installation_settings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "installation_settings");
        }
    }
}
