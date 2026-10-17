using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TCMine.Server.Infrastructure.Persistence;

#nullable disable

namespace TCMine.Server.Infrastructure.Postgres.Migrations
{
    /// <summary>
    ///     Escrita à mão, e sem Designer: não muda o modelo, só apaga as linhas
    ///     que apontam para servidores e modpacks que já não existem. Até aqui
    ///     estas quatro tabelas não tinham chave estrangeira, e apagar um
    ///     servidor ou um pack deixava-as para trás ("Ver vínculos" mostrava
    ///     "(servidor removido)"). Precisa de correr ANTES da migration que cria
    ///     as chaves: com órfãos na tabela, o Postgres recusa a chave e a
    ///     migration inteira falha.
    /// </summary>
    [DbContext(typeof(TcMineDbContext))]
    [Migration("20261017063000_DeleteOrphanLinks")]
    public partial class DeleteOrphanLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "memberships" WHERE "GameServerId" NOT IN (SELECT "Id" FROM "game_servers");
                DELETE FROM "invites" WHERE "GameServerId" NOT IN (SELECT "Id" FROM "game_servers");
                DELETE FROM "modpack_memberships" WHERE "ModpackId" NOT IN (SELECT "Id" FROM "modpacks");
                DELETE FROM "news" WHERE "ModpackId" NOT IN (SELECT "Id" FROM "modpacks");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nada a desfazer: as linhas apagadas apontavam para o que já não
            // existia, e não há de onde as reconstruir.
        }
    }
}
