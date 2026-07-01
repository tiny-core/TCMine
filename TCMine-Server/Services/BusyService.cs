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

    // Log de passos da operação atual (ex.: cada etapa do provisionamento). Cap para não crescer sem fim.
    private const int MaxSteps = 40;
    private readonly List<string> _steps = [];

    /// <summary>Mensagem da operação em andamento (a da última iniciada); null quando ocioso.</summary>
    public string? Message { get; private set; }

    /// <summary>
    /// Histórico de passos da operação atual, do primeiro ao mais recente (o último é o passo em curso).
    /// Operações de um só passo têm um item; o provisionamento acumula vários. Vazio quando ocioso.
    /// </summary>
    public IReadOnlyList<string> Steps => _steps;

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

    /// <summary>
    /// Atualiza a mensagem do overlay durante uma operação em andamento (progresso ao vivo). No-op se
    /// nada estiver ocupado. Útil como destino de um <see cref="IProgress{T}"/> — ex.: provisionamento
    /// de servidor reportando cada etapa. Seguro chamar de qualquer thread (reagenda via OnChange).
    /// </summary>
    public void Update(string message)
    {
        if (_activeCount <= 0) return;
        Message = message;
        AppendOrCoalesceStep(message);
        OnChange?.Invoke();
    }

    // Adiciona o passo ao log — ou substitui o último quando é a MESMA etapa se atualizando ao vivo.
    // Convenção: atualizações ao vivo (ex.: "% de download", "25/120 mods") usam " — " para separar o
    // rótulo fixo do detalhe variável; passos com o mesmo rótulo (prefixo antes do " — ") coalescem
    // numa linha só em vez de inundar o log.
    private void AppendOrCoalesceStep(string message)
    {
        static string Label(string s)
        {
            var i = s.IndexOf(" — ", StringComparison.Ordinal);
            return i < 0 ? s : s[..i];
        }

        if (_steps.Count > 0 && Label(_steps[^1]) == Label(message))
            _steps[^1] = message;
        else
            _steps.Add(message);

        if (_steps.Count > MaxSteps)
            _steps.RemoveRange(0, _steps.Count - MaxSteps);
    }

    /// <summary>
    /// <see cref="IProgress{T}"/> que escreve no overlay — passe ao serviço para refletir o progresso
    /// ao vivo. <see cref="Progress{T}"/> reagenda o callback no contexto de sincronização capturado.
    /// </summary>
    public IProgress<string> Progress()
    {
        return new Progress<string>(Update);
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
        // Só a operação de topo semeia/limpa o log (aninhadas apenas somam o contador)
        if (_activeCount == 0)
        {
            _steps.Clear();
            _steps.Add(message);
        }

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
            _steps.Clear();
        }

        OnChange?.Invoke();
    }
}