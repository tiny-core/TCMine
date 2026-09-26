using TCMine.Contracts;
using TCMine.Contracts.Handshake;

namespace TCMine.Launcher.Core.Connectivity;

/// <summary>
///     Descobre — ou estabelece — a que servidor este launcher pertence.
///     É o primeiro caso de uso que roda, antes de qualquer tela útil: sem
///     servidor não há catálogo, não há login e não há o que jogar.
/// </summary>
public sealed class ServerPairing(IHandshakeClient handshake, ILauncherConfigProvider config)
{
    /// <summary>
    ///     Retoma o pareamento gravado, se houver, e confirma que o servidor
    ///     ainda fala a nossa língua.
    /// </summary>
    public async Task<PairingState> ResumeAsync(CancellationToken ct)
    {
        var saved = await config.TryLoadAsync(ct);

        if (saved is null)
            return PairingState.NotPaired();

        var result = await handshake.PerformAsync(saved.ServerUrl, ct);

        // Note que o config vai junto mesmo na falha: servidor fora do ar não
        // desfaz pareamento, e apagá-lo aqui mandaria o jogador redigitar o
        // endereço a cada oscilação de rede.
        var atual = result.Outcome is HandshakeOutcome.Ok
            ? await RefreshAsync(saved, result.Response!, ct)
            : saved;

        return PairingState.FromHandshake(result, atual);
    }

    /// <summary>
    ///     Adota o que o servidor acabou de dizer sobre si mesmo.
    ///     Sem isto o pareamento congela no dia em que aconteceu: o client ID do
    ///     Azure e o nome ficam gravados para sempre, e um administrador que
    ///     corrija o registo no painel não alcança quem já pareou — o launcher
    ///     continua a abrir o navegador com o id antigo, que é o sintoma que
    ///     revelou isto.
    ///     Só grava quando algo mudou de facto: escrever a cada arranque seria
    ///     I/O inútil no caminho mais quente do launcher.
    ///     Um valor em branco NÃO substitui o que está gravado. Servidor sem
    ///     login configurado é estado transitório — apagar o id por causa dele
    ///     trocaria "o administrador ainda não configurou" por um ficheiro que o
    ///     próprio launcher recusa a carregar no arranque seguinte.
    /// </summary>
    private async Task<LauncherConfig> RefreshAsync(
        LauncherConfig saved,
        HandshakeResponse response,
        CancellationToken ct)
    {
        var clientId = string.IsNullOrWhiteSpace(response.AzureClientId)
            ? saved.AzureClientId
            : response.AzureClientId;

        var name = string.IsNullOrWhiteSpace(response.ServerName)
            ? saved.DisplayName
            : response.ServerName;

        if (clientId == saved.AzureClientId && name == saved.DisplayName)
            return saved;

        var novo = saved with { AzureClientId = clientId, DisplayName = name };

        await config.SaveAsync(novo, ct);

        return novo;
    }

    /// <summary>
    ///     Pareia com o endereço que o jogador digitou. Grava o tcmine.json
    ///     apenas se o servidor responder e for compatível.
    /// </summary>
    public async Task<PairingState> PairAsync(string address, CancellationToken ct)
    {
        if (!TryNormalize(address, out var url, out var error))
            return PairingState.Rejected(error);

        var result = await handshake.PerformAsync(url, ct);

        if (result.Outcome is not HandshakeOutcome.Ok)
            return PairingState.FromHandshake(result, null);

        var response = result.Response!;

        if (string.IsNullOrWhiteSpace(response.AzureClientId))
        {
            // O servidor está de pé e fala o nosso protocolo, mas sem o client id
            // do Azure não há como autenticar ninguém — e o LauncherConfig se
            // recusa a gravar assim, de propósito. Sem traduzir aqui, o jogador
            // leria "AzureClientId ausente" e concluiria que o launcher está
            // quebrado, quando quem tem de agir é o administrador.
            return PairingState.Rejected(
                $"O servidor {response.ServerName} respondeu, mas ainda não está "
                + "configurado para o login de jogadores. Avise o administrador.");
        }

        var novo = new LauncherConfig
        {
            Schema = 1,
            ServerUrl = url,

            // O client id vem do servidor, não do instalador: é ele quem sabe
            // contra qual app do Azure os jogadores dele autenticam.
            AzureClientId = response.AzureClientId,
            DisplayName = response.ServerName
        };

        var problemas = novo.Validate();

        if (problemas.Count > 0)
            return PairingState.Rejected(string.Join(" ", problemas));

        await config.SaveAsync(novo, ct);

        return PairingState.Paired(novo, response);
    }

    /// <summary>
    ///     Transforma o que foi digitado numa URL utilizável, ou explica por quê
    ///     não dá. A validação de transporte acontece AQUI, antes do handshake:
    ///     recusar depois significaria ter mandado o pedido por HTTP puro.
    /// </summary>
    private static bool TryNormalize(string address, out Uri url, out string error)
    {
        url = null!;
        error = "";

        var texto = address.Trim();

        if (texto.Length is 0)
        {
            error = "Informe o endereço do servidor.";
            return false;
        }

        // Quem digita "meuservidor.com" quer https. Exigir o esquema completo
        // apenas transformaria o engano mais comum numa mensagem de erro.
        if (!texto.Contains("://", StringComparison.Ordinal))
            texto = "https://" + texto;

        if (!Uri.TryCreate(texto, UriKind.Absolute, out var candidato))
        {
            error = "Endereço inválido.";
            return false;
        }

        if (candidato.Scheme != Uri.UriSchemeHttp && candidato.Scheme != Uri.UriSchemeHttps)
        {
            error = "O endereço precisa ser um site (https://).";
            return false;
        }

        if (!LauncherConfig.IsSecureTransport(candidato))
        {
            // Não é preciosismo: o id_token da Microsoft trafega nesta conexão.
            error = "O endereço precisa usar HTTPS. Em HTTP puro, qualquer um na "
                    + "mesma rede consegue ler os dados da sua conta.";
            return false;
        }

        url = candidato;
        return true;
    }
}
