using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Common;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Domain.Settings;

/// <summary>
///     Configuração operacional da instalação, editável pelo painel.
///     Linha única: é a configuração DESTE TCMine, não uma coleção.
///     O que muda no deploy (conexão do banco, endpoint do Docker, caminho dos
///     blobs) continua em appsettings — mexer nisso exige reiniciar de qualquer
///     forma, e não deve depender do banco estar de pé.
/// </summary>
public sealed class InstallationSettings : Entity
{
    // ---------- Padrões para novos modpacks ----------

    /// <summary>Versão do Minecraft já selecionada ao criar um modpack. Nula = sem padrão.</summary>
    public string? DefaultMinecraftVersion { get; set; }

    public ModLoader DefaultLoader { get; set; } = ModLoader.NeoForge;

    /// <summary>RAM sugerida (MB) para novas versões.</summary>
    public int DefaultMemoryMb { get; set; } = 4096;

    /// <summary>
    ///     Quantos backups AUTOMÁTICOS manter por servidor. Zero = ilimitado.
    ///     Só os automáticos expiram: um snapshot manual foi um ato deliberado do
    ///     admin — apagá-lo por política seria o painel decidindo que o trabalho
    ///     dele valia menos que espaço em disco. Cinco cobre alguns rollbacks
    ///     seguidos sem deixar dezenas de GB para trás.
    /// </summary>
    public int WorldBackupKeepCount { get; set; } = 5;

    // ---------- Servidores de jogo ----------

    /// <summary>
    ///     Faixa de onde sai a porta de um servidor novo (inclusive nas duas
    ///     pontas). Contínua de propósito: um único redirecionamento de faixa no
    ///     roteador cobre os servidores de hoje e os que vierem.
    /// </summary>
    public int GamePortRangeStart { get; set; } = GamePortDefaults.First;

    public int GamePortRangeEnd { get; set; } = GamePortDefaults.Last;

    /// <summary>
    ///     Host pelo qual os jogadores alcançam esta máquina: um domínio ou DDNS,
    ///     sem porta. Nulo = usar o IP público detectado.
    ///     Existe ao lado da detecção porque o IP de uma ligação residencial muda,
    ///     e um nome não: com ele preenchido, a troca de IP não invalida o
    ///     endereço que os jogadores já têm.
    /// </summary>
    public string? PublicHost { get; set; }

    // ---------- DNS dos servidores (Cloudflare) ----------

    /// <summary>
    ///     Token da API da Cloudflare, cifrado em repouso como a chave do
    ///     CurseForge e, como ela, nunca devolvido à UI. Precisa só da permissão
    ///     de editar DNS na zona abaixo.
    /// </summary>
    public string? CloudflareApiTokenEncrypted { get; set; }

    /// <summary>Id da zona na Cloudflare (32 caracteres hexadecimais). Não é segredo.</summary>
    public string? CloudflareZoneId { get; set; }

    /// <summary>
    ///     Domínio sob o qual os servidores ganham nome ("exemplo.com", ou um
    ///     nível abaixo: "jogos.exemplo.com"). Tem de estar dentro da zona.
    /// </summary>
    public string? DnsBaseDomain { get; set; }

    /// <summary>
    ///     Rótulo do registro A que o TCMine mantém apontado para o IP público
    ///     ("mc" → mc.exemplo.com), e para onde os SRV apontam. Nulo = o padrão,
    ///     <see cref="GameDns.DefaultHostLabel" />. Não é usado quando o endereço
    ///     público é um nome: aí os SRV apontam para ele.
    /// </summary>
    public string? DnsHostLabel { get; set; }

    /// <summary>
    ///     Há o bastante para manter registros: token, zona e domínio. Faltando
    ///     um, os subdomínios dos servidores ficam guardados mas não são
    ///     publicados nem usados no endereço.
    /// </summary>
    public bool DnsEnabled =>
        !string.IsNullOrWhiteSpace(CloudflareApiTokenEncrypted)
        && !string.IsNullOrWhiteSpace(CloudflareZoneId)
        && !string.IsNullOrWhiteSpace(DnsBaseDomain);

    // ---------- Integrações ----------

    /// <summary>
    ///     Chave da API do CurseForge, cifrada em repouso. Sem ela, o resolver do
    ///     CurseForge se declara indisponível e o sistema segue só com Modrinth.
    ///     Nunca é devolvida à UI — só se informa se existe ou não.
    /// </summary>
    public string? CurseForgeApiKeyEncrypted { get; set; }

    // ---------- Login com a Microsoft (MSAL, no launcher) ----------

    /// <summary>
    ///     Client ID da app Azure contra a qual os jogadores autenticam.
    ///     Vive aqui, e não em appsettings, porque registrar a app no Azure é
    ///     uma etapa que acontece DEPOIS do deploy, feita pela mesma pessoa que
    ///     cola a chave do CurseForge na mesma tela. Estando em arquivo, o
    ///     sintoma era: o jogador pareia, ouve "avise o administrador", e o
    ///     administrador precisa entrar no container, editar JSON e reiniciar.
    ///     Ao contrário da chave do CurseForge, NÃO é
    ///     segredo e não é cifrado: o fluxo do Minecraft usa public client com
    ///     PKCE, o id viaja no handshake para qualquer launcher que pergunte, e
    ///     por isso volta normalmente para a tela.
    ///     Nulo enquanto ninguém configurou.
    /// </summary>
    public string? AzureClientId { get; set; }

    // Não há mais e-mail: o SMTP e o servidor de e-mail próprio serviam à
    // recuperação de senha, que deixou de existir quando o login passou a ser
    // só pela Microsoft. Um subsistema sem consumidor só pesava (segredo
    // cifrado, container com a porta 587, uma aba inteira de configuração).
}
