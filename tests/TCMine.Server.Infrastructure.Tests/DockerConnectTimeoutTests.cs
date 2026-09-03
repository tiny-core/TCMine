using System.Diagnostics;
using Microsoft.Extensions.Options;
using TCMine.Server.Infrastructure.Docker;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Falar com um Docker que não está lá falha DEPRESSA.
///     Existe por um bug que nenhum teste via, porque nada estava errado no
///     resultado: com o Docker Desktop parado, a página de Definições carregava
///     em cem segundos e mostrava "não criado" — a resposta certa, no fim de uma
///     espera absurda. A causa é o named pipe: ConnectAsync espera
///     indefinidamente o pipe aparecer, e quem interrompia era o timeout padrão
///     do HttpClient.
/// </summary>
public sealed class DockerConnectTimeoutTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pipe_inexistente_falha_dentro_do_prazo_de_conexao()
    {
        // Named pipe é uma coisa do Windows, e o modo de falhar também: no Linux
        // um socket Unix inexistente já devolve erro na hora, então o teste não
        // teria o que provar.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "O caso é do named pipe do Windows.");

        var factory = new DockerHttpClientFactory(Options.Create(new DockerOptions
        {
            Endpoint = @"npipe://./pipe/tcmine-pipe-que-nao-existe",
            ConnectTimeout = TimeSpan.FromSeconds(1)
        }));

        using var client = factory.Create();

        var relogio = Stopwatch.StartNew();

        await Should.ThrowAsync<HttpRequestException>(
            async () => await client.GetAsync("/v1.45/containers/json", Ct));

        relogio.Stop();

        // Folga generosa sobre o prazo de 1s, para não ficar frágil numa máquina
        // ocupada. O que o teste tranca é a ordem de grandeza: segundos, e não os
        // cem do timeout do HttpClient.
        relogio.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
    }
}
