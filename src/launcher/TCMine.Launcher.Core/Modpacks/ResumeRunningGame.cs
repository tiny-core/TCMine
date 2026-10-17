using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Reencontra, no arranque, o jogo que um launcher anterior deixou aberto.
///     Sem isto, fechar o launcher (pela bandeja, ou por um crash dele) e
///     reabri-lo dava uma tela que não sabe do jogo: "Jogar" ligado, e um clique
///     abria uma segunda cópia sobre o mesmo mundo.
///     O que volta é o processo, não a ligação: a saída dele morreu com o
///     launcher antigo (ver <see cref="IGameProcessFinder" />), e um túnel da
///     Cloudflare, se havia, também — vivia dentro daquele processo.
/// </summary>
public sealed class ResumeRunningGame(
    IRunningGameStore records,
    IGameProcessFinder finder,
    IInstanceStore instances,
    GameSession session)
{
    /// <returns>Verdadeiro quando há um jogo reencontrado.</returns>
    public async Task<bool> HandleAsync(CancellationToken ct)
    {
        if (session.IsRunning)
            return false;

        var record = await records.ReadAsync(ct);
        if (record is null)
            return false;

        var manifest = await instances.ReadManifestAsync(record.Instance, ct);
        var path = instances.PathFor(record.Instance);

        // Instância removida, processo que já saiu, ou PID que agora é de outro
        // programa: o registo é velho. Apagado, para não ser lido de novo a cada
        // arranque.
        if (manifest is null || finder.TryReattach(record.Process, path) is not { } process)
        {
            await records.ClearAsync(ct);
            return false;
        }

        if (session.Reattach(new InstalledInstance(record.Instance, manifest, 0, path), process))
            return true;

        process.Dispose();
        return false;
    }
}
