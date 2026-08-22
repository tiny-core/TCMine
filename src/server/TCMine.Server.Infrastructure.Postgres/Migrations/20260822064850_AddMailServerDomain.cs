using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddMailServerDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MailServerDomain",
                table: "installation_settings",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MailServerDomain",
                table: "installation_settings");
        }
    }
}
