namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Quem é o jogador, segundo o Minecraft.
///     O nome e o UUID vinham do <c>LauncherSessionDto</c> — do servidor TCMine —
///     e isso era conveniência, não desenho: a identidade que o JOGO exige é a do
///     perfil do Minecraft, e o nosso servidor apenas a repassava. A consequência
///     era séria: com o servidor fora do ar não se abria o jogo, mesmo com
///     internet e com a conta Microsoft a responder normalmente.
/// </summary>
public interface IPlayerProfileSource
{
    /// <summary>
    ///     O perfil da conta a que o token pertence, ou nulo se não der para
    ///     saber. Nulo não é erro: sem rede é o esperado, e quem chama recorre ao
    ///     que ficou guardado da última vez.
    /// </summary>
    Task<PlayerProfile?> GetAsync(string accessToken, CancellationToken ct);
}

/// <summary>
///     Guarda o último perfil conhecido, para jogar sem rede nenhuma.
///     O que fica em disco é nome e UUID — não o token. Um token vale cerca de
///     uma hora e guardá-lo trocaria "expira sozinho" por "fica à espera de quem
///     o leia"; nome e UUID não são segredo e são exatamente o que o jogo mostra
///     a toda a gente numa partida.
/// </summary>
public interface IPlayerProfileCache
{
    Task<PlayerProfile?> ReadAsync(CancellationToken ct);

    Task WriteAsync(PlayerProfile profile, CancellationToken ct);
}

/// <summary>Nome e UUID, como o Minecraft os entende.</summary>
public sealed record PlayerProfile(string Name, string Uuid);
