using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Decide a porta do host de um servidor de jogo.
///     É uma porta (e não lógica do caso de uso) porque "livre" depende de duas
///     coisas que a Application não enxerga juntas: os outros servidores no banco
///     e o que mais está publicado no Docker desta máquina.
/// </summary>
public interface IGamePortAllocator
{
    /// <summary>A primeira porta livre da faixa configurada.</summary>
    Task<Result<int>> AllocateAsync(CancellationToken ct);

    /// <summary>
    ///     Confere uma porta escolhida à mão. <paramref name="exceptServerId" /> é
    ///     o servidor que está sendo editado: a porta que ele já tem não conta
    ///     como ocupada por ele mesmo.
    /// </summary>
    Task<Result> EnsureAvailableAsync(int port, Guid? exceptServerId, CancellationToken ct);
}
