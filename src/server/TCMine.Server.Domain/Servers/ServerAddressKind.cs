namespace TCMine.Server.Domain.Servers;

/// <summary>
///     Como os jogadores chegam a UM servidor — uma escolha só.
///     Antes, o endereço escrito e o subdomínio conviviam no formulário, e o
///     escrito vencia em silêncio: quem preenchia os dois quase sempre queria o
///     subdomínio, e recebia o outro. Não é coluna: sai de qual dos dois campos
///     está preenchido (<see cref="GameAddress.KindOf" />), e os casos de uso
///     recusam os dois juntos. O túnel (fase 4B.2) entra aqui como quarta opção.
/// </summary>
public enum ServerAddressKind
{
    /// <summary>O endereço da instalação (Configurações → Rede) mais a porta do servidor.</summary>
    Installation,

    /// <summary>Um nome próprio na Cloudflare, sem porta.</summary>
    Subdomain,

    /// <summary>Um endereço escrito pelo admin (outro domínio, ou uma porta redirecionada).</summary>
    Manual
}
