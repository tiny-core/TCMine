using Microsoft.Data.Sqlite;
using TCMine.Server.Web.Configuration;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests;

/// <summary>
///     Pasta que falta é o estado normal de uma instalação nova, de um volume
///     recém-montado e de quem apagou <c>data/</c> para recomeçar. O SQLite abre
///     o arquivo mas não cria o diretório, e a mensagem dele — "unable to open
///     database file" — não menciona pasta nenhuma, então o sintoma some do
///     radar de quem não conhece o detalhe.
/// </summary>
public sealed class StoragePathsTests
{
    [Theory]
    [InlineData("Data Source=data/tcmine.db", "data")]
    [InlineData("Data Source=./data/blobs/x.db", "./data/blobs")]
    [InlineData("Data Source=/var/lib/tcmine/app.db", "/var/lib/tcmine")]
    public void Extrai_o_diretorio_do_arquivo_do_banco(string connectionString, string esperado)
    {
        StoragePaths.SqliteDirectoryOf(connectionString)
            .ShouldBe(esperado.Replace('/', Path.DirectorySeparatorChar));
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=tcmine.db")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Host=localhost;Database=tcmine")]
    public void Nao_inventa_diretorio_quando_nao_ha_o_que_criar(string? connectionString)
    {
        // Banco em memória, arquivo solto na pasta de trabalho, string vazia e
        // connection string de outro provider: em nenhum deles existe pasta a
        // criar, e criar uma pelo nome errado espalharia diretórios pelo disco.
        StoragePaths.SqliteDirectoryOf(connectionString).ShouldBeNull();
    }

    [Fact]
    public async Task Aplicacao_sobe_com_a_pasta_do_banco_apagada()
    {
        // A reprodução do bug: apagar data/ e iniciar. Antes disto, o arranque
        // morria no primeiro acesso ao banco.
        var raiz = Path.Combine(Path.GetTempPath(), $"tcmine-pastas-{Guid.CreateVersion7():N}");
        var pastaBanco = Path.Combine(raiz, "data");

        try
        {
            Directory.Exists(pastaBanco).ShouldBeFalse("a pasta não deve existir antes do arranque");

            await using var factory = new TcMineAppFactory(
                settings:
                [
                    ("Database:ConnectionString", $"Data Source={Path.Combine(pastaBanco, "tcmine.db")}"),
                    ("BlobStorage:RootPath", Path.Combine(raiz, "blobs"))
                ]);

            // Uma requisição qualquer que toque o banco: se a pasta faltasse, o
            // arranque teria falhado antes de responder.
            var resposta = await factory.CreateClient().GetAsync(
                "/health/live", TestContext.Current.CancellationToken);

            resposta.IsSuccessStatusCode.ShouldBeTrue();

            Directory.Exists(pastaBanco).ShouldBeTrue();
            Directory.Exists(Path.Combine(raiz, "blobs")).ShouldBeTrue();
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            try
            {
                if (Directory.Exists(raiz))
                    Directory.Delete(raiz, true);
            }
            catch (IOException)
            {
                // Limpeza não reprova teste; o sistema recolhe o temporário.
            }
        }
    }
}
