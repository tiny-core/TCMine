using TCMine.Contracts.Modpacks;

namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Abre o Minecraft. Tudo o que é decidido ANTES já chegou decidido aqui.
///     A porta existe porque abrir o jogo é o trabalho mais sujo do launcher —
///     índice da Mojang, bibliotecas, assets, nativos, classpath, e um JSON de
///     versão por loader — e nada disso é decisão de produto. O que é decisão
///     mora no <c>LaunchGame</c>, do lado de cá: qual Java, qual token, o que
///     dizer quando falta alguma coisa.
/// </summary>
public interface IGameLauncher
{
    Task<GameLaunchResult> LaunchAsync(
        GameLaunchRequest request,
        IProgress<GameLaunchProgress>? progress,
        CancellationToken ct);
}

/// <summary>
///     Tudo o que abrir o jogo exige, já resolvido.
///     É um pacote e não uma dúzia de parâmetros porque a lista cresce a cada
///     loader suportado, e um construtor com doze strings na mesma ordem é um
///     bug de troca esperando acontecer — <c>PlayerName</c> e
///     <c>PlayerUuid</c> são ambos string e ninguém repara.
/// </summary>
public sealed record GameLaunchRequest
{
    /// <summary>Pasta da instância. Vira o <c>--gameDir</c> do jogo.</summary>
    public required string InstanceDirectory { get; init; }

    public required string MinecraftVersion { get; init; }

    public required ModLoader Loader { get; init; }

    /// <summary>Versão do loader. Nula (ou vazia) só faz sentido em Vanilla.</summary>
    public string? LoaderVersion { get; init; }

    /// <summary>Executável resolvido pelo <see cref="IJavaLocator" />.</summary>
    public required string JavaPath { get; init; }

    public required string PlayerName { get; init; }

    /// <summary>UUID minúsculo e sem hífens, como o servidor o devolve.</summary>
    public required string PlayerUuid { get; init; }

    /// <summary>
    ///     Access token do Minecraft, recém-obtido — ou NULO para abrir em modo
    ///     offline, quando não houve como falar com a Microsoft.
    ///     Nunca é guardado: é readquirido a cada abertura, porque vale cerca de
    ///     uma hora e guardá-lo trocaria "expira sozinho" por "fica no disco à
    ///     espera de quem o leia".
    ///     Sem token o jogo abre para um jogador só: entrar em servidores online
    ///     exige prova de conta, e essa é uma regra do Minecraft, não nossa.
    /// </summary>
    public string? AccessToken { get; init; }

    public int? MemoryMb { get; init; }
}

public sealed record GameLaunchProgress(string Phase, double? Fraction = null);

/// <summary>
///     Se o jogo abriu, e o que dizer quando não.
///     O sucesso é "o processo arrancou", não "o jogador está a jogar": um crash
///     três segundos depois é assunto do <see cref="Process" />, não desta chamada.
/// </summary>
public sealed record GameLaunchResult(bool Started, string? Message, IGameProcess? Process = null)
{
    public static GameLaunchResult Ok(IGameProcess process) => new(true, null, process);

    public static GameLaunchResult Failed(string message) => new(false, message);
}
