using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddImportRequester : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RequestedBy",
                table: "import_requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "modpacks" SET "OwnerId" = (
                    SELECT "Id" FROM "users" WHERE "IsInstanceAdmin" ORDER BY "CreatedAt", "Id" LIMIT 1)
                WHERE "OwnerId" = '00000000-0000-0000-0000-000000000000'
                  AND EXISTS (SELECT 1 FROM "users" WHERE "IsInstanceAdmin");

                UPDATE "modpack_memberships" SET "Role" = 'Owner'
                WHERE "Role" <> 'Owner'
                  AND "UserId" = (SELECT p."OwnerId" FROM "modpacks" p WHERE p."Id" = "modpack_memberships"."ModpackId")
                  AND NOT EXISTS (
                      SELECT 1 FROM "modpack_memberships" o
                      WHERE o."ModpackId" = "modpack_memberships"."ModpackId" AND o."Role" = 'Owner');

                INSERT INTO "modpack_memberships" ("Id", "CreatedAt", "UserId", "ModpackId", "Role")
                SELECT
                    printf('%s-%s-4%s-%s%s-%s',
                        hex(randomblob(4)), hex(randomblob(2)), substr(hex(randomblob(2)), 2),
                        substr('89AB', abs(random()) % 4 + 1, 1), substr(hex(randomblob(2)), 2), hex(randomblob(6))),
                    strftime('%Y-%m-%d %H:%M:%f', 'now') || '+00:00',
                    p."OwnerId", p."Id", 'Owner'
                FROM "modpacks" p
                WHERE EXISTS (SELECT 1 FROM "users" u WHERE u."Id" = p."OwnerId")
                  AND NOT EXISTS (SELECT 1 FROM "modpack_memberships" m WHERE m."ModpackId" = p."Id" AND m."Role" = 'Owner');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedBy",
                table: "import_requests");
        }
    }
}
