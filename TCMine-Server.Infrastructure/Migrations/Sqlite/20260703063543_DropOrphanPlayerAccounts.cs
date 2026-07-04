using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Server.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class DropOrphanPlayerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tabela órfã do antigo login brokered pelo servidor (removido em favor do MSAL no launcher).
            // A migration que a criava foi apagada do histórico, então este drop é idempotente:
            // remove-a onde ficou sobrando e é no-op em bancos que nunca a tiveram.
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"PlayerAccounts\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não recriamos uma tabela órfã sem esquema; nada a reverter.
        }
    }
}
