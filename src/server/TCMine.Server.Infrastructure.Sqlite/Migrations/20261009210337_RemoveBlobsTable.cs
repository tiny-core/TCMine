using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBlobsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blobs",
                columns: table => new
                {
                    Sha256 = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastAccessedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blobs", x => x.Sha256);
                });
        }
    }
}
