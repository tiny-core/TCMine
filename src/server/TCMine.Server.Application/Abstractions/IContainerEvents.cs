namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Os containers dos servidores de jogo mudando de estado, no momento em que
///     acontece — e não quando alguém abre uma página.
///     Antes não havia nada disto: o status só era conferido no arranque e ao
///     abrir as telas de servidores, então um servidor que caía ficava "no ar"
///     até alguém olhar, e o "caiu" do registro de atividade tinha a hora em que
///     se percebeu, não a da queda.
///     Separado do <see cref="IServerOrchestrator" /> de propósito: é uma
///     assinatura de longa duração, com ciclo de vida próprio, e não mais um
///     comando sobre um servidor.
/// </summary>
public interface IContainerEvents
{
    /// <summary>
    ///     O id do servidor cujo container acabou de mudar de estado, um por
    ///     evento, enquanto a ligação com o Docker durar. Termina, ou lança,
    ///     quando ela cai (o daemon reiniciou, por exemplo); quem assina volta a
    ///     assinar.
    ///     Só diz QUEM mudou, não o novo estado: quem decide o estado continua a
    ///     ser <see cref="IServerOrchestrator.GetStatusAsync" />, uma regra só.
    /// </summary>
    IAsyncEnumerable<Guid> WatchAsync(CancellationToken ct);
}
