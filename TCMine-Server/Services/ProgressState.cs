namespace TCMine_Server.Services;

/// <summary>
///     Estado observável de progresso para diálogos de feedback (ex.: import de modpack): segura a
///     mensagem corrente e notifica quem renderiza a cada atualização. Passado como parâmetro a um
///     diálogo bloqueante, permite atualizá-lo ao vivo — um <see cref="IProgress{T}" /> escreve aqui e o
///     diálogo re-renderiza.
///     Existe porque o <c>IDialogReference</c> do MudBlazor não expõe troca de parâmetros após exibir:
///     o diálogo subscreve <see cref="OnChange" /> em vez de receber novos parâmetros.
/// </summary>
public sealed class ProgressState(string initial = "")
{
    /// <summary>Mensagem atual exibida pelo diálogo.</summary>
    public string Message { get; private set; } = initial;

    /// <summary>Disparado a cada atualização da mensagem para o diálogo re-renderizar.</summary>
    public event Action? OnChange;

    /// <summary>Atualiza a mensagem e notifica. Alvo natural de um <see cref="Progress{T}" />.</summary>
    public void Report(string message)
    {
        Message = message;
        OnChange?.Invoke();
    }
}