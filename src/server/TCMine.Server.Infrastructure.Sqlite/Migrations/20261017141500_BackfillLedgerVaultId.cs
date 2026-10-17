using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TCMine.Server.Infrastructure.Persistence;

#nullable disable

namespace TCMine.Server.Infrastructure.Sqlite.Migrations
{
    /// <summary>
    ///     Escrita à mão, e sem Designer: não muda o modelo, só preenche a nuvem
    ///     das linhas do ledger gravadas antes da coluna existir. A
    ///     AddLedgerVaultId cria a coluna com o GUID vazio em todas elas, e
    ///     uma linha sem nuvem some da auditoria da nuvem inteira. Precisa de
    ///     correr DEPOIS da gerada (é a coluna dela que preenche), por isso o
    ///     horário logo a seguir. A nuvem sai do canal da linha: é a mesma regra
    ///     que o código usa nas linhas novas, e todo canal tem nuvem (FK). Só
    ///     toca nas linhas com o GUID vazio, então rodar de novo não muda nada.
    /// </summary>
    [DbContext(typeof(TcMineDbContext))]
    [Migration("20261017141500_BackfillLedgerVaultId")]
    public partial class BackfillLedgerVaultId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "cloud_ledger" SET "VaultId" = (
                    SELECT c."VaultId" FROM "cloud_channels" AS c
                    WHERE c."Id" = "cloud_ledger"."ChannelId")
                WHERE "VaultId" = '00000000-0000-0000-0000-000000000000';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nada a desfazer: o Down da AddLedgerVaultId apaga a coluna inteira.
        }
    }
}
