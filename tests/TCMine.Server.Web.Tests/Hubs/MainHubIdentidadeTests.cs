using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TCMine.Contracts.Hubs;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Hubs;

/// <summary>
///     Quem o hub pensa que está falando com ele.
///     Existe porque a identidade do MainHub vinha do <c>IHttpContextAccessor</c>,
///     e uma conexão WebSocket não tem HttpContext durante a invocação — só
///     durante a negociação. O bug é invisível por long polling (cada invocação
///     chega numa requisição HTTP nova, que tem contexto) e fatal por WebSocket,
///     que é justamente o transporte que o launcher vai usar.
/// </summary>
public sealed class MainHubIdentidadeTests
{
    [Theory]
    [InlineData(HttpTransportType.LongPolling)]
    [InlineData(HttpTransportType.WebSockets)]
    public async Task Admin_da_instalacao_e_reconhecido_em_qualquer_transporte(HttpTransportType transporte)
    {
        await using var factory = new TcMineAppFactory();
        var cookie = await factory.EntrarComoAdminAsync();

        await using var conexao = ConectarAsync(factory, cookie, transporte);
        await conexao.StartAsync(TestContext.Current.CancellationToken);

        // Id inexistente de propósito: o admin da instalação é Owner de tudo
        // sem ir ao banco, então o único jeito de isto falhar é o hub não
        // enxergar o usuário. Assinar um servidor que existisse confundiria
        // "não sei quem você é" com "este servidor não existe".
        var acao = async () => await conexao.InvokeAsync(
            nameof(IServerHub.SubscribeServerAsync),
            Guid.CreateVersion7());

        await acao.ShouldNotThrowAsync();
    }

    private static HubConnection ConectarAsync(
        TcMineAppFactory factory,
        string cookie,
        HttpTransportType transporte) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, HubRoutes.Main), options =>
            {
                options.Transports = transporte;
                options.Headers["Cookie"] = cookie;

                // Sem o handler do TestServer a conexão sairia para a rede de
                // verdade e não acharia ninguém: a app só existe em memória.
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();

                options.WebSocketFactory = async (contexto, ct) =>
                {
                    var ws = factory.Server.CreateWebSocketClient();
                    ws.ConfigureRequest = requisicao => requisicao.Headers.Cookie = cookie;
                    return await ws.ConnectAsync(contexto.Uri, ct);
                };
            })
            .Build();
}
