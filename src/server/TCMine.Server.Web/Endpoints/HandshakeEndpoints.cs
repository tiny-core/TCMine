using Microsoft.Extensions.Options;
using TCMine.Contracts;
using TCMine.Contracts.Handshake;
using TCMine.Server.Application.Abstractions;
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
        // Comandos de console traduzidos para RCON pelo servidor: papel,
        // allowlist e execução.
        Capabilities.ConsoleCommands,

        // Linhas do console empurradas para quem assina o servidor. Só entrou
        // depois de existir quem as bombeasse — anunciar antes faria o launcher
        // exibir um console que nunca recebe nada.
        Capabilities.ConsoleStream,

        // Upload manual de mod: o AddManualFile e o diálogo existem desde
        // antes, e a capability só nunca tinha sido declarada.
        Capabilities.ManualModUpload
    ];

    public static IEndpointRouteBuilder MapHandshake(this IEndpointRouteBuilder app)
    {
        app.MapGet(Protocol.HandshakeRoute, async (
                IOptions<ServerOptions> options,
                ISettingsRepository settings,
                CancellationToken ct) =>
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
                    AzureClientId = await ResolveAzureClientIdAsync(settings, server, ct),
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

    /// <summary>
    ///     De onde sai o client id do Azure: painel primeiro, appsettings depois.
    ///     A ordem não é arbitrária. Registrar a app no Azure acontece DEPOIS do
    ///     deploy, então o lugar natural do valor é a tela de configurações — e
    ///     lido daqui, a mudança vale no próximo handshake, sem reiniciar o
    ///     processo com jogadores conectados. O appsettings continua valendo como
    ///     semente para que instalação já configurada por arquivo (ou por
    ///     variável de ambiente, no Docker) siga de pé sem ninguém tocar em nada.
    /// </summary>
    private static async Task<string> ResolveAzureClientIdAsync(
        ISettingsRepository settings,
        ServerOptions server,
        CancellationToken ct)
    {
        var stored = (await settings.GetAsync(ct)).AzureClientId;

        return string.IsNullOrWhiteSpace(stored) ? server.AzureClientId : stored;
    }
}
