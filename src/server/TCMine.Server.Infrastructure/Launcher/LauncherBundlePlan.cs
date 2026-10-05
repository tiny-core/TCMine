using TCMine.Server.Application.Updates;

namespace TCMine.Server.Infrastructure.Launcher;

/// <summary>O que fazer com o canal ao publicar o launcher embutido.</summary>
public enum LauncherBundleAction
{
    /// <summary>Já está lá, com esta versão e este endereço.</summary>
    Skip,

    /// <summary>O canal tem versão maior (publicada à mão): não rebaixa.</summary>
    KeepNewer,

    /// <summary>Versão nova: empacota por cima do canal, e o vpk gera o delta.</summary>
    Append,

    /// <summary>
    ///     Mesma versão com outro endereço (ou publicada à mão sem endereço): o
    ///     vpk recusa a mesma versão duas vezes, então o canal é refeito do zero.
    ///     Quem já tem essa versão não baixa nada — o número não mudou.
    /// </summary>
    Rebuild
}

/// <summary>
///     A decisão, separada do I/O para ter teste: é ela que garante que um
///     arranque comum não reempacota nada, e que um feed publicado à mão com
///     versão maior não é atropelado pela imagem.
/// </summary>
public static class LauncherBundlePlan
{
    /// <param name="bundledVersion">A versão que veio na imagem.</param>
    /// <param name="serverUrl">O endereço a embutir agora (vazio = genérico).</param>
    /// <param name="publishedVersion">A maior versão no canal, ou nulo.</param>
    /// <param name="stamp">O que a última publicação embutida registrou, ou nulo.</param>
    public static LauncherBundleAction Decide(
        string bundledVersion, string serverUrl, string? publishedVersion, LauncherBundleStamp? stamp)
    {
        if (publishedVersion is null || !SemanticVersion.TryParse(publishedVersion, out var published))
            return LauncherBundleAction.Append;

        if (!SemanticVersion.TryParse(bundledVersion, out var bundled))
            return LauncherBundleAction.Skip;

        if (published > bundled)
            return LauncherBundleAction.KeepNewer;

        if (published < bundled)
            return LauncherBundleAction.Append;

        return stamp == new LauncherBundleStamp(bundledVersion, serverUrl)
            ? LauncherBundleAction.Skip
            : LauncherBundleAction.Rebuild;
    }
}

/// <summary>O que foi publicado da última vez: versão e endereço embutido.</summary>
public sealed record LauncherBundleStamp(string Version, string ServerUrl)
{
    public const string FileName = ".tcmine-bundle";

    public static LauncherBundleStamp? Read(string channelDir)
    {
        var path = Path.Combine(channelDir, FileName);
        if (!File.Exists(path))
            return null;

        var lines = File.ReadAllLines(path);
        return lines.Length >= 2 ? new LauncherBundleStamp(lines[0], lines[1]) : null;
    }

    public void Write(string channelDir) =>
        File.WriteAllLines(Path.Combine(channelDir, FileName), [Version, ServerUrl]);
}
