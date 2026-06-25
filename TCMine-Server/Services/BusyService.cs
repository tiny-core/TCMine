namespace TCMine_Server.Services;

/// <summary>
/// Estado de "ocupado" do circuito Blazor: centraliza o feedback bloqueante de operações
/// assíncronas (gravações no banco, chamadas a serviços, etc.). As páginas envolvem a operação
/// em <see cref="RunAsync(string, Func{Task})"/> e um único <c>BusyOverlay</c> no layout reage ao
/// estado — não há um modal por página.
///
/// Scoped (um por circuito): o overlay e as páginas compartilham a mesma instância. Usa um contador
/// para suportar operações sobrepostas/aninhadas — o overlay só some quando a última terminar.
/// </summary>
public sealed class BusyService
{
    // Quantas operações estão em andamento agora (aninhadas/concorrentes somam)
    private int _activeCount;

    /// <summary>Mensagem da operação em andamento (a da última iniciada); null quando ocioso.</summary>
    public string? Message { get; private set; }

    /// <summary>Há ao menos uma operação em andamento?</summary>
    public bool IsBusy => _activeCount > 0;

    /// <summary>Disparado a cada mudança de estado para o overlay re-renderizar.</summary>
    public event Action? OnChange;

    /// <summary>Executa uma operação assíncrona sem retorno sob o overlay bloqueante.</summary>
    public async Task RunAsync(string message, Func<Task> operation)
    {
        // Reusa a sobrecarga genérica para não duplicar o begin/end
        await RunAsync(message, async () =>
        {
            await operation();
            return true;
        });
    }

    /// <summary>Executa uma operação assíncrona com retorno sob o overlay bloqueante.</summary>
    public async Task<T> RunAsync<T>(string message, Func<Task<T>> operation)
    {
        Begin(message);
        // Cede o contexto para o Blazor renderizar e enviar o overlay ao cliente ANTES de começar o
        // trabalho — assim a primeira coisa que o usuário vê é a modal, mesmo que a operação trave a
        // thread de render em seguida (ex.: montar uma tabela/árvore grande).
        await Task.Yield();
        try
        {
            return await operation();
        }
        finally
        {
            // Sempre libera, mesmo se a operação lançar — a página trata a exceção (snackbar)
            End();
        }
    }

    private void Begin(string message)
    {
        _activeCount++;
        Message = message;
        OnChange?.Invoke();
    }

    private void End()
    {
        if (--_activeCount <= 0)
        {
            _activeCount = 0;
            Message = null;
        }

        OnChange?.Invoke();
    }
}