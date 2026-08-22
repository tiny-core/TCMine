using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
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
                type: "TEXT",
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
