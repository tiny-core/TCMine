namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Garante que existe um JRE da versão certa e devolve o caminho do executável.
///     Nós gerenciamos o Java, não o sistema. Deixar o autodetect escolher é
///     receita para achar um Java 8 esquecido no PATH e passar a tarde
///     investigando um erro que não tem nada a ver com o modpack.
///     Minecraft 1.20.5 ou superior exige Java 21.
///     Minecraft 1.17 a 1.20.4 exige Java 17.
/// </summary>
public interface IJavaLocator
{
    Task<string> EnsureRuntimeAsync(
        int majorVersion,
        IProgress<double>? progress,
        CancellationToken ct);
}
