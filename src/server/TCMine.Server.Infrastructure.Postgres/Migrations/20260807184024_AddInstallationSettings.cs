using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultMinecraftVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DefaultLoader = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultMemoryMb = table.Column<int>(type: "integer", nullable: false),
                    CurseForgeApiKeyEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SmtpHost = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SmtpPort = table.Column<int>(type: "integer", nullable: false),
                    SmtpUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SmtpPasswordEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SmtpFrom = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SmtpUseTls = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
