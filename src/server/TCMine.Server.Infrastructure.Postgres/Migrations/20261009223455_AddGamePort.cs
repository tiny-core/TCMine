using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddGamePort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GamePortRangeEnd",
                table: "installation_settings",
                type: "integer",
                nullable: false,
                defaultValue: 25599);

            migrationBuilder.AddColumn<int>(
                name: "GamePortRangeStart",
                table: "installation_settings",
                type: "integer",
                nullable: false,
                defaultValue: 25565);

            migrationBuilder.AddColumn<int>(
                name: "GamePort",
                table: "game_servers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // A porta que o servidor JÁ usava: o que vinha depois do ":" no
            // endereço, ou 25565. Assim nenhum servidor muda de porta sozinho.
            migrationBuilder.Sql(
                """
                UPDATE "game_servers" SET "GamePort" = CASE
                    WHEN "ConnectAddress" ~ ':[0-9]{1,5}$'
                         AND substring("ConnectAddress" from ':([0-9]+)$')::int BETWEEN 1 AND 65535
                    THEN substring("ConnectAddress" from ':([0-9]+)$')::int
                    ELSE 25565
                END;
                """);

            // Duplicados: o servidor mais antigo fica com a porta, e os demais
            // recebem portas novas acima da maior em uso. Sem isto o índice
            // único abaixo recusaria a migration inteira.
            migrationBuilder.Sql(
                """
                WITH d AS (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "GamePort" ORDER BY "Id") AS rn FROM "game_servers"
                ), x AS (
                    SELECT "Id", ROW_NUMBER() OVER (ORDER BY "Id") AS k FROM d WHERE rn > 1
                ), m AS (
                    SELECT MAX("GamePort") AS mx FROM "game_servers"
                )
                UPDATE "game_servers" g SET "GamePort" = m.mx + x.k FROM x, m WHERE g."Id" = x."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_game_servers_GamePort",
                table: "game_servers",
                column: "GamePort",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_game_servers_GamePort",
                table: "game_servers");

            migrationBuilder.DropColumn(
                name: "GamePortRangeEnd",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "GamePortRangeStart",
                table: "installation_settings");

            migrationBuilder.DropColumn(
                name: "GamePort",
                table: "game_servers");
        }
    }
}
