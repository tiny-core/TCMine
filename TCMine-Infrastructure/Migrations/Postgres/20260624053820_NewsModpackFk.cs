using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class NewsModpackFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ModpackId",
                table: "News",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_News_ModpackId",
                table: "News",
                column: "ModpackId");

            migrationBuilder.AddForeignKey(
                name: "FK_News_Modpacks_ModpackId",
                table: "News",
                column: "ModpackId",
                principalTable: "Modpacks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_News_Modpacks_ModpackId",
                table: "News");

            migrationBuilder.DropIndex(
                name: "IX_News_ModpackId",
                table: "News");

            migrationBuilder.DropColumn(
                name: "ModpackId",
                table: "News");
        }
    }
}
