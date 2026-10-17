using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TCMine.Server.Infrastructure.Persistence;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <summary>
    ///     Escrita à mão, e sem Designer: não muda o modelo, só cria as marcas
    ///     d'água a partir dos lotes que já existiam. Até a AddCloudWatermarks a
    ///     régua do hello era o maior lote APLICADO de cada (nuvem, servidor,
    ///     jogador); sem este passo a tabela nasce vazia e a detecção de rollback
    ///     não enxergaria nada do que veio antes. Precisa de correr DEPOIS da
    ///     gerada (é a tabela dela que preenche), por isso o horário logo a seguir.
    ///     - "Maior" compara a época antes da sequência, como o domínio.
    ///     - Só servidores que ainda existem: a marca tem FK em cascata para o
    ///       servidor, e lote de servidor apagado não tem mundo para comparar.
    ///     - O Id da marca é o Id do lote que a originou: é único (um lote por
    ///       chave) e dá o mesmo SQL nos dois bancos, sem função de UUID.
    ///     - Marca que já existe não é tocada, então rodar de novo não muda nada.
    /// </summary>
    [DbContext(typeof(TcMineDbContext))]
    [Migration("20261017152500_BackfillCloudWatermarks")]
    public partial class BackfillCloudWatermarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "cloud_watermarks" ("Id", "VaultId", "ServerId", "PlayerUuid", "Epoch", "Seq", "CreatedAt")
                SELECT b."Id", b."VaultId", b."ServerId", b."PlayerUuid", b."Epoch", b."Seq", b."CreatedAt"
                FROM "cloud_batches" AS b
                WHERE b."Status" = 'Applied'
                  AND EXISTS (SELECT 1 FROM "game_servers" AS s WHERE s."Id" = b."ServerId")
                  AND NOT EXISTS (
                    SELECT 1 FROM "cloud_batches" AS n
                    WHERE n."VaultId" = b."VaultId" AND n."ServerId" = b."ServerId"
                      AND n."PlayerUuid" = b."PlayerUuid" AND n."Status" = 'Applied'
                      AND (n."Epoch" > b."Epoch" OR (n."Epoch" = b."Epoch" AND n."Seq" > b."Seq")))
                  AND NOT EXISTS (
                    SELECT 1 FROM "cloud_watermarks" AS w
                    WHERE w."VaultId" = b."VaultId" AND w."ServerId" = b."ServerId"
                      AND w."PlayerUuid" = b."PlayerUuid");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nada a desfazer: o Down da AddCloudWatermarks apaga a tabela inteira.
        }
    }
}
