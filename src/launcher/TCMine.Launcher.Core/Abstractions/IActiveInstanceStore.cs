using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Qual instância o jogador escolheu jogar.
///     Ficheiro próprio, e não um campo no <c>tcmine.json</c>, de propósito. O
///     tcmine.json é a identidade do servidor e o launcher recusa-se a arrancar
///     sem ele válido; misturar ali uma preferência volátil significaria que
///     corromper a preferência perde o pareamento — e o jogador volta a digitar o
///     endereço por causa de um clique em "jogar".
/// </summary>
public interface IActiveInstanceStore
{
    /// <summary>
    ///     A escolha guardada, ou nulo quando não há.
    ///     Nulo é o caso normal da primeira instalação, e é também o que se
    ///     devolve quando o ficheiro está ilegível: a escolha reconstrói-se com
    ///     um clique, e recusar a tela de jogar por causa dela seria trocar um
    ///     incómodo por um impedimento.
    /// </summary>
    Task<InstanceKey?> ReadAsync(CancellationToken ct);

    Task WriteAsync(InstanceKey key, CancellationToken ct);
}
