using System.Text.Json.Serialization;

namespace TCMine.Contracts;

/// <summary>
///     Conteúdo do tcmine.json, gerado pelo server no momento do download.
///     Mora na RAIZ do diretório de instalação (%LOCALAPPDATA%\TCMine\), nunca
///     dentro de current\ — o Velopack substitui current\ inteira a cada update
///     e o arquivo sumiria no primeiro autoupdate.
/// </summary>
public sealed record LauncherConfig
{
    /// <summary>Versão do schema deste arquivo.</summary>
    public required int Schema { get; init; }

    /// <summary>URL base do TCMine Server. Deve ser HTTPS fora de localhost.</summary>
    public required Uri ServerUrl { get; init; }

    /// <summary>
    ///     Client ID da app Azure. É público por natureza: o fluxo do Minecraft
    ///     usa public client com PKCE, não existe client secret para vazar.
    /// </summary>
    public required string AzureClientId { get; init; }

    /// <summary>Nome exibido na janela do launcher.</summary>
    public string? DisplayName { get; init; }

    public Uri? BrandingIconUrl { get; init; }

    [JsonIgnore]
    public bool IsTransportSecure => IsSecureTransport(ServerUrl);

    /// <summary>
    ///     A mesma regra, aplicável antes de existir configuração.
    ///     A tela de pareamento precisa dela para recusar um endereço ANTES de
    ///     falar com ele: validar só na hora de gravar significaria ter mandado
    ///     o handshake por HTTP puro primeiro.
    /// </summary>
    public static bool IsSecureTransport(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps || url.IsLoopback;

    /// <summary>
    ///     Retorna a lista de problemas. Vazia significa configuração válida.
    ///     A checagem de HTTPS não é preciosismo: o id_token da Microsoft trafega
    ///     nesta conexão. Em HTTP puro, qualquer um na mesma rede o intercepta.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var erros = new List<string>();

        if (Schema is not 1)
            erros.Add($"Schema {Schema} não é suportado por esta versão do launcher.");

        if (!ServerUrl.IsAbsoluteUri)
            erros.Add("ServerUrl precisa ser uma URL absoluta.");
        else if (!IsTransportSecure)
            erros.Add("ServerUrl precisa usar HTTPS (exceção apenas para localhost).");

        if (string.IsNullOrWhiteSpace(AzureClientId))
            erros.Add("AzureClientId ausente.");

        return erros;
    }
}
