namespace TCMine_Application.Launcher;

/// <summary>
/// "Live link": escuta o servidor (SSE <c>/events</c>) e avisa quando o conteúdo público muda (modpacks
/// editados/criados/removidos no admin) e quando a ligação muda de estado. A implementação reconecta
/// sozinha; os eventos podem vir de uma thread de fundo (o consumidor marshala para a UI).
/// </summary>
public interface IContentWatcher
{
    /// <summary>O conteúdo do servidor mudou — recarregar catálogo/instância ativa.</summary>
    event Action? ContentChanged;

    /// <summary>Estado da ligação SSE mudou (true = ligado).</summary>
    event Action<bool>? ConnectionChanged;

    void Start();
    void Stop();
}
