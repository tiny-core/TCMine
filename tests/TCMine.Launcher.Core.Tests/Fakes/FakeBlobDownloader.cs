using System.Text;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>Downloader falso: devolve bytes previsíveis e registra o que pediram.</summary>
public sealed class FakeBlobDownloader : IBlobDownloader
{
    public List<string> Requested { get; } = [];

    public Task<Stream> OpenAsync(Uri serverUrl, string sha256, CancellationToken ct)
    {
        Requested.Add(sha256);

        return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes($"conteudo-{sha256}")));
    }
}
