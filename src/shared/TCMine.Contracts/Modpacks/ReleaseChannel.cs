namespace TCMine.Contracts.Modpacks;

/// <summary>
///     Em que canal uma versão vive.
///     Não é um campo gravado em lado nenhum: sai do PRÓPRIO número da versão,
///     por SemVer — qualquer coisa depois do hífen é pré-lançamento. Guardá-lo
///     à parte criaria um segundo lugar para a verdade, e o dia em que os dois
///     discordassem seria um pack a atualizar para o canal errado.
/// </summary>
public enum ReleaseChannel
{
    /// <summary>Versões estáveis. É o que uma instalação normal recebe.</summary>
    Release,

    /// <summary>
    ///     Pré-lançamentos. Um canal fechado: uma instância alpha atualiza para
    ///     outra alpha e nunca sai daí, e não se cria instância nova a partir
    ///     dela — quem quer estável instala estável pelo catálogo.
    /// </summary>
    Alpha
}

public static class ReleaseChannels
{
    /// <summary>
    ///     O canal de uma versão ("1.4.0" → Release, "1.5.0-beta" → Alpha).
    ///     Regra única do sistema: o domínio do servidor usa-a para o seu
    ///     IsPreRelease, e o launcher para saber em que canal cada instância está.
    /// </summary>
    public static ReleaseChannel Of(string? version) =>
        version?.Contains('-', StringComparison.Ordinal) is true
            ? ReleaseChannel.Alpha
            : ReleaseChannel.Release;
}
