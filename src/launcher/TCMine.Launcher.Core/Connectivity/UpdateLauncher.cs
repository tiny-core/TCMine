using TCMine.Contracts.Handshake;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Connectivity;

/// <summary>
///     Decide se o launcher deve atualizar-se, e manda.
///     A decisão é de produto e por isso mora aqui, testável sem empacotamento
///     nenhum: o mecanismo — baixar e substituir ficheiros — está atrás do
///     <see cref="ILauncherUpdater" />.
/// </summary>
public sealed class UpdateLauncher(ILauncherUpdater updater)
{
    /// <summary>
    ///     Verdadeiro quando a aplicação vai reiniciar para aplicar a atualização.
    ///     Recebe a resposta do handshake porque é ela que traz as três coisas
    ///     que decidem: onde está o feed, e se o administrador congelou as
    ///     atualizações.
    /// </summary>
    public async Task<bool> HandleAsync(HandshakeResponse? server, CancellationToken ct)
    {
        // Sem handshake não há feed. Acontece com o servidor fora do ar, e
        // atualizar às cegas por uma URL guardada seria ir buscar binários a um
        // endereço que já ninguém confirmou.
        if (server is null)
            return false;

        // O congelamento existe para um evento em curso: ninguém atualiza no
        // meio da partida. É do administrador, e não se discute aqui.
        if (server.UpdatesFrozen)
            return false;

        if (server.LauncherFeedUrl is not { } feed)
            return false;

        return await updater.UpdateAsync(feed, ct);
    }
}
