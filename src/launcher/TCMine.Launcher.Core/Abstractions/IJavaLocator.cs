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

    /// <summary>
    ///     Os JREs que estão no disco, com o que cada um ocupa.
    ///     O tamanho vem junto porque é ele que justifica apagar: "remover Java
    ///     não utilizado" sem um número ao lado é um botão que ninguém clica.
    /// </summary>
    Task<IReadOnlyList<InstalledRuntime>> ListAsync(CancellationToken ct);

    /// <summary>
    ///     Apaga um JRE. Puro cache: o custo de errar é voltar a descarregá-lo,
    ///     e nenhum dado do jogador vive aqui.
    /// </summary>
    Task RemoveAsync(int majorVersion, CancellationToken ct);
}

public sealed record InstalledRuntime(int MajorVersion, long SizeBytes);
