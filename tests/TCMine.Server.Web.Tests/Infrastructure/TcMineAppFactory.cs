using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace TCMine.Server.Web.Tests.Infrastructure;

/// <summary>
///     Sobe a aplicação inteira em memória, com o pipeline real.
///     Existe porque três comportamentos que já quebraram (ou quase) não moram em
///     classe nenhuma: o limite de taxa é middleware, o health check é endpoint, e
///     a validação de configuração acontece no arranque. Testá-los por unidade
///     provaria que as constantes têm o valor certo, não que o pipeline as usa.
///     Cada instância recebe seu próprio arquivo SQLite em pasta temporária: as
///     migrations rodam no arranque em Development, então o banco nasce pronto e
///     nenhum teste enxerga a base de desenvolvimento.
/// </summary>
internal sealed class TcMineAppFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), $"tcmine-teste-{Guid.CreateVersion7():N}.db");

    private readonly string _environment;
    private readonly (string Key, string Value)[] _settings;

    public TcMineAppFactory(
        string environment = "Development",
        params (string Key, string Value)[] settings)
    {
        _environment = environment;
        _settings = settings;
    }

    /// <summary>
    ///     Troca de serviços para o teste. Existe para substituir portas que
    ///     saem para fora — a verificação do perfil na Mojang, por exemplo:
    ///     depender dela de verdade tornaria a suíte refém de uma API externa.
    /// </summary>
    public Action<IServiceCollection>? Servicos { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        // Erros detalhados no hub. Sem isto, uma exceção qualquer dentro de um
        // método do hub chega ao cliente como um HubException genérico e SEM
        // mensagem — foi exatamente assim que uma falha intermitente do
        // InviteFlow apareceu no CI, sem dizer nada sobre a causa. Vale só em
        // teste: em produção a mensagem interna não deve sair para o cliente.
        builder.ConfigureTestServices(services =>
            services.Configure<HubOptions>(o => o.EnableDetailedErrors = true));

        if (Servicos is not null)
            builder.ConfigureTestServices(Servicos);

        // Obrigatório fora de Development, e nada aqui testa o login do
        // launcher: sem um padrão, toda suíte que sobe em Production teria de
        // saber desta chave.
        builder.UseSetting("Server:AzureClientId", "client-id-de-teste");

        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:ConnectionString", $"Data Source={_databasePath}");

        // Os ajustes do teste vêm por último de propósito: um caso que precise de
        // banco inacessível ou de configuração inválida tem de conseguir passar
        // por cima do padrão saudável montado acima.
        foreach (var (key, value) in _settings)
            builder.UseSetting(key, value);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        // Sem isto o arquivo NUNCA é apagado: o provider devolve a conexão ao
        // pool em vez de fechá-la, e o handle sobrevive à queda do host. Cada
        // execução da suíte deixava uma dúzia de bancos para trás.
        SqliteConnection.ClearAllPools();

        // O -wal e o -shm acompanham o banco quando o journal está em WAL.
        foreach (var arquivo in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
        {
            try
            {
                if (File.Exists(arquivo))
                    File.Delete(arquivo);
            }
            catch (IOException)
            {
                // Limpeza não é motivo para reprovar um teste; o SO recolhe.
            }
        }
    }
}
