using Microsoft.Extensions.Options;
using TCMine.Contracts;
using TCMine.Contracts.Handshake;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Endpoints;

/// <summary>
///     O único endpoint com formato congelado do sistema.
///     É por ele que launcher e servidor descobrem se conseguem conversar. Se
///     este contrato mudar, um launcher antigo não consegue nem exibir a
///     mensagem dizendo que está desatualizado — ele falha na desserialização
///     antes disso.
///     Por isso: nunca remova nem renomeie campo aqui. Só acrescente opcionais.
/// </summary>
public static class HandshakeEndpoints
{
    /// <summary>
    ///     O que esta build sabe fazer.
    ///     A lista cresce conforme as funcionalidades ficam prontas. O launcher
    ///     esconde o botão correspondente quando a capability não aparece aqui —
    ///     e é isso que permite publicar uma feature no cliente antes do servidor.
    /// </summary>
    private static readonly string[] CurrentCapabilities =
    [
        // Ainda nenhuma: vamos adicionando conforme implementamos.
    ];

    public static IEndpointRouteBuilder MapHandshake(this IEndpointRouteBuilder app)
    {
        app.MapGet(Protocol.HandshakeRoute, (IOptions<ServerOptions> options) =>
            {
                var server = options.Value;

                // O canal do Velopack deriva do PROTOCOLO, não da versão do
                // produto. É o que permite publicar launcher 1.6.0, 1.7.0, 1.8.0 e ter
                // todos chegando aos clientes sem release nenhum do servidor.
                var channel = $"win-x64-p{Protocol.Current}";

                var response = new HandshakeResponse
                {
                    ProtocolMin = Protocol.MinimumSupported,
                    ProtocolMax = Protocol.Current,
                    ServerVersion = ThisAssembly.Version,
                    ServerName = server.Name,
                    LauncherChannel = channel,
                    LauncherFeedUrl = new Uri(
                        server.PublicUrl ?? new Uri("https://localhost"),
                        $"/updates/launcher/{channel}/"),
                    MinLauncherVersion = server.MinLauncherVersion,
                    UpdatesFrozen = server.FreezeLauncherUpdates,
                    AzureClientId = server.AzureClientId,
                    Capabilities = CurrentCapabilities
                };

                return Results.Ok(response);
            })
            .WithName("Handshake")
            // Anônimo de propósito: o launcher precisa saber se é compatível
            // antes de tentar autenticar.
            .AllowAnonymous();

        return app;
    }
}