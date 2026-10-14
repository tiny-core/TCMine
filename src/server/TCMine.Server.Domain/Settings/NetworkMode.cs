namespace TCMine.Server.Domain.Settings;

/// <summary>
///     Como os jogadores encontram esta máquina — uma escolha só, e não três
///     grupos de campos que se anulam sem avisar.
///     Antes, o endereço público, o IP detectado e a Cloudflare conviviam na
///     mesma tela, e quem vencia dependia de quais estavam preenchidos: o admin
///     preenchia um, o outro continuava a valer, e nada na tela dizia qual.
/// </summary>
public enum NetworkMode
{
    /// <summary>O IP público detectado, mais a porta de cada servidor.</summary>
    Automatic,

    /// <summary>Um domínio ou DDNS do admin (ou um IP fixo), mais a porta.</summary>
    OwnHost,

    /// <summary>
    ///     Domínio na Cloudflare: o TCMine mantém um registro A com o IP e um SRV
    ///     por servidor com subdomínio.
    /// </summary>
    Cloudflare
}
