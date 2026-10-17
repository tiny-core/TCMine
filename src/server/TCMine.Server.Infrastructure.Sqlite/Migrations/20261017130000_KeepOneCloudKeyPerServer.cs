using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TCMine.Server.Infrastructure.Persistence;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <summary>
    ///     Escrita à mão, e sem Designer: não muda o modelo, só deixa UMA chave
    ///     por servidor em cloud_server_credentials. Até aqui cada start
    ///     acrescentava uma linha e revogava a anterior, que nunca saía. Precisa
    ///     de correr ANTES da migration que torna o GameServerId único: com
    ///     duplicados na tabela, o índice é recusado e a migration inteira falha.
    ///     A linha que fica é a chave ATIVA (é a que o servidor de jogo em
    ///     execução está usando agora; apagá-la cortaria a nuvem dele até o
    ///     próximo start) ou, sem ativa, a mais recente. Antes de apagar, o
    ///     último mundo conhecido é copiado para ela: é o que a detecção de
    ///     rollback compara no hello seguinte.
    /// </summary>
    [DbContext(typeof(TcMineDbContext))]
    [Migration("20261017130000_KeepOneCloudKeyPerServer")]
    public partial class KeepOneCloudKeyPerServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "cloud_server_credentials" SET "WorldId" = (
                    SELECT w."WorldId" FROM "cloud_server_credentials" AS w
                    WHERE w."GameServerId" = "cloud_server_credentials"."GameServerId" AND w."WorldId" IS NOT NULL
                    ORDER BY w."Id" DESC
                    LIMIT 1)
                WHERE "WorldId" IS NULL;

                DELETE FROM "cloud_server_credentials"
                WHERE "RevokedAt" IS NOT NULL
                  AND EXISTS (
                    SELECT 1 FROM "cloud_server_credentials" AS a
                    WHERE a."GameServerId" = "cloud_server_credentials"."GameServerId" AND a."RevokedAt" IS NULL);

                DELETE FROM "cloud_server_credentials"
                WHERE EXISTS (
                    SELECT 1 FROM "cloud_server_credentials" AS n
                    WHERE n."GameServerId" = "cloud_server_credentials"."GameServerId"
                      AND n."Id" > "cloud_server_credentials"."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nada a desfazer: as linhas apagadas eram chaves revogadas, que não
            // abriam nada, e não há de onde as reconstruir.
        }
    }
}
