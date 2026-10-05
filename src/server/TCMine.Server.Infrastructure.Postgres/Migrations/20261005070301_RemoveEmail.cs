using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MailServerDomain",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "SmtpFrom",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "SmtpPasswordEncrypted",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "SmtpUseTls",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "SmtpUser",
                table: "installation_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MailServerDomain",
                table: "installation_settings",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpFrom",
                table: "installation_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                table: "installation_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPasswordEncrypted",
                table: "installation_settings",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "installation_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpUseTls",
                table: "installation_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUser",
                table: "installation_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }
    }
}
