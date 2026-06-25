using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Infrastructure.Migrations.Postgres;

/// <inheritdoc />
public partial class NewsModpackFk : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            "ModpackId",
            "News",
            "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            "IX_News_ModpackId",
            "News",
            "ModpackId");

        migrationBuilder.AddForeignKey(
            "FK_News_Modpacks_ModpackId",
            "News",
            "ModpackId",
            "Modpacks",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            "FK_News_Modpacks_ModpackId",
            "News");

        migrationBuilder.DropIndex(
            "IX_News_ModpackId",
            "News");

        migrationBuilder.DropColumn(
            "ModpackId",
            "News");
    }
}