namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>
///     Coletor de progresso que corre NO MESMO fio de quem reporta.
///     O <see cref="Progress{T}" /> da BCL posta no contexto de sincronização, e
///     num teste isso é uma corrida: a asserção acontece antes de a callback
///     correr. Em isolamento o fio está livre e o teste passa; com a suíte cheia
///     perde a corrida sempre — foi assim que um teste de progresso passou verde
///     durante meses sem nunca ter verificado nada.
///     Um <see cref="IProgress{T}" /> escrito à mão não posta: chama direto.
/// </summary>
public sealed class ProgressoSincrono<T> : IProgress<T>
{
    public List<T> Relatado { get; } = [];

    public void Report(T value) => Relatado.Add(value);
}
