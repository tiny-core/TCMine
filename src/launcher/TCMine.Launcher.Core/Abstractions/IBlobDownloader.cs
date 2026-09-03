namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Traz os bytes de um arquivo do content store do servidor.
///     Resolve por HASH e não por URL: a origem é detalhe de implementação, e é
///     o hash que diz se o arquivo está correto. O mesmo endereço serve todos os
///     modpacks, e é isso que faz um mod compartilhado entre packs ser baixado
///     uma vez só.
/// </summary>
public interface IBlobDownloader
{
    /// <summary>
    ///     Abre o conteúdo. Quem chama é dono do stream e o descarta.
    ///     O tamanho conhecido vem do manifesto, então o progresso pode ser
    ///     exibido sem depender de Content-Length.
    /// </summary>
    Task<Stream> OpenAsync(Uri serverUrl, string sha256, CancellationToken ct);
}
