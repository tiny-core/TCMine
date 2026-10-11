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
    ///     As portas que um servidor pode receber agora: as livres da faixa
    ///     configurada. É o que o formulário oferece — escolher de uma lista do
    ///     que serve, em vez de digitar um número e descobrir depois que não
    ///     servia.
    /// </summary>
    /// <param name="exceptServerId">
    ///     O servidor em edição: a porta que o container dele publica hoje não
    ///     conta como ocupada.
    /// </param>
    /// <param name="max">Teto de opções devolvidas.</param>
    Task<GamePortChoices> ListChoicesAsync(Guid? exceptServerId, int max, CancellationToken ct);

    /// <summary>
    ///     Confere uma porta escolhida à mão: tem de estar na faixa configurada e
    ///     livre. <paramref name="exceptServerId" /> é
    ///     o servidor que está sendo editado: a porta que ele já tem não conta
    ///     como ocupada por ele mesmo.
    /// </summary>
    Task<Result> EnsureAvailableAsync(int port, Guid? exceptServerId, CancellationToken ct);
}

/// <param name="Free">Portas livres da faixa, da menor para a maior, até o teto pedido.</param>
/// <param name="Truncated">Há mais portas livres do que as devolvidas.</param>
public sealed record GamePortChoices(int RangeStart, int RangeEnd, IReadOnlyList<int> Free, bool Truncated);
