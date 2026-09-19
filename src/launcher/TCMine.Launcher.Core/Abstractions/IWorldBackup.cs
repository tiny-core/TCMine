using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Cópias do mundo do jogador, antes de mexer na instância.
///     O mundo é a única coisa numa instância que não se baixa de novo. Mods,
///     configs e o próprio Minecraft voltam do servidor ou da Mojang; uma base
///     construída durante meses, não.
/// </summary>
public interface IWorldBackup
{
    /// <summary>
    ///     Há mundo nesta instância.
    ///     Decide se vale oferecer cópia: propor backup de uma instalação que
    ///     nunca foi jogada ensina o jogador a ignorar o aviso.
    /// </summary>
    bool HasWorld(InstanceKey key);

    /// <summary>
    ///     Copia os mundos para um ficheiro, e devolve onde ficou.
    ///     Fora da pasta da instância de propósito: é o instalador que a
    ///     reescreve, e guardar a cópia lá dentro seria guardá-la no único sítio
    ///     que a operação seguinte pode mexer.
    /// </summary>
    Task<string> CreateAsync(InstanceKey key, CancellationToken ct);
}
