namespace TCMine_Application.Launcher;

/// <summary>
///     Porta de autoupdate do launcher: consome o feed Velopack servido pelo TCMine Server em
///     <c>/updates</c> (o mesmo que o servidor gera ao compilar o launcher). A implementação usa o
///     <c>UpdateManager</c> do Velopack; só faz sentido quando o aplicativo foi <b>instalada</b> (não em dev).
/// </summary>
public interface IUpdateService
{
    /// <summary>O aplicativo foi instalada via Velopack? (rodando de uma build de dev, updates não se aplicam).</summary>
    bool IsInstalled { get; }

    /// <summary>Verifica o feed; devolve a versão disponível (maior que a atual) ou <c>null</c>.</summary>
    Task<string?> CheckAsync(CancellationToken ct = default);

    /// <summary>
    ///     Baixa e aplica a última atualização verificada e <b>reinicia</b> a aplicativo (não retorna se aplicar).
    ///     <paramref name="progress" /> recebe o percentual de download (0–100).
    /// </summary>
    Task DownloadAndApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default);
}