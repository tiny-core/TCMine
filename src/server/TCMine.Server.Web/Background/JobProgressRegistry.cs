using System.Collections.Concurrent;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Onde vive o progresso dos trabalhos em background. Singleton de propósito:
///     o estado precisa sobreviver à navegação e ser visível de qualquer página —
///     é isso que permite o admin sair da tela do modpack, ir ao painel e
///     continuar vendo a importação andar, em vez de perder o acompanhamento.
///     Quem escreve são os workers (pela porta <see cref="IJobProgressReporter" />);
///     quem lê são os componentes, que assinam <see cref="Changed" /> e re-renderizam
///     por empurrão, sem sondar o banco.
/// </summary>
public sealed class JobProgressRegistry : IJobProgressReporter
{
    private readonly ConcurrentDictionary<Guid, JobProgress> _active = new();

    /// <summary>
    ///     Últimos encerramentos, para a UI conseguir mostrar "terminou" mesmo que
    ///     o componente só renderize depois. Limitado — não é histórico.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, string?> _finished = new();

    public IReadOnlyCollection<KeyValuePair<Guid, JobProgress>> Active => _active.ToArray();

    /// <summary>Disparado a cada mudança. Assinantes devem re-renderizar via InvokeAsync.</summary>
    public event Action? Changed;

    /// <summary>
    ///     Um cancelamento por trabalho, ligado ao desligamento da aplicação.
    ///     Vive aqui pelo mesmo motivo do progresso: o admin que mandou parar
    ///     pode ter saído da página, e o botão de cancelar precisa funcionar de
    ///     qualquer tela.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellations = new();

    public bool IsRunning(Guid scopeId) => _active.ContainsKey(scopeId);

    /// <summary>Este trabalho aceita ser cancelado?</summary>
    public bool IsCancellable(Guid scopeId) => _cancellations.ContainsKey(scopeId);

    /// <summary>
    ///     Abre o cancelamento de um trabalho e devolve o token que ele deve
    ///     respeitar. Ligado ao <paramref name="applicationStopping" /> para que
    ///     um deploy no meio do trabalho não fique esperando por ele.
    /// </summary>
    public CancellationToken BeginCancellable(Guid scopeId, CancellationToken applicationStopping)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);

        // Se sobrou um do trabalho anterior no mesmo escopo, ele não serve mais.
        if (_cancellations.TryRemove(scopeId, out var anterior))
            anterior.Dispose();

        _cancellations[scopeId] = cts;
        Changed?.Invoke();

        return cts.Token;
    }

    /// <summary>Pede o cancelamento. Quem executa é que decide como parar.</summary>
    public void Cancel(Guid scopeId)
    {
        if (_cancellations.TryGetValue(scopeId, out var cts))
            cts.Cancel();
    }

    /// <summary>Fecha o cancelamento — o trabalho acabou, cancelado ou não.</summary>
    public void EndCancellable(Guid scopeId)
    {
        if (_cancellations.TryRemove(scopeId, out var cts))
            cts.Dispose();

        Changed?.Invoke();
    }

    public void Report(Guid scopeId, JobProgress progress)
    {
        _active[scopeId] = progress;
        Changed?.Invoke();
    }

    public void Complete(Guid scopeId, string? error = null)
    {
        _active.TryRemove(scopeId, out _);
        _finished[scopeId] = error;
        Changed?.Invoke();
    }

    public JobProgress? Get(Guid scopeId) => _active.GetValueOrDefault(scopeId);

    /// <summary>
    ///     Consome o encerramento de um trabalho: devolve true se ele acabou desde
    ///     a última pergunta, com o erro (nulo em sucesso). Consumir evita a página
    ///     recarregar em loop ao ver o mesmo encerramento a cada render.
    /// </summary>
    public bool TryConsumeCompletion(Guid scopeId, out string? error) =>
        _finished.TryRemove(scopeId, out error);
}
