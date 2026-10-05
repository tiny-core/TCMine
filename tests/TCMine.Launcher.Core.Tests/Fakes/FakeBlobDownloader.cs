using System.Text;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>
///     Downloader falso: devolve bytes previsíveis e registra o que pediram.
///     Seguro para chamadas simultâneas, porque a instalação baixa em paralelo;
///     com <see cref="Delay" /> cada download fica "em voo" o bastante para o
///     teste medir quantos correram ao mesmo tempo.
/// </summary>
public sealed class FakeBlobDownloader : IBlobDownloader
{
    private readonly Lock _gate = new();
    private int _emVoo;

    public List<string> Requested { get; } = [];

    public TimeSpan Delay { get; init; } = TimeSpan.Zero;

    /// <summary>O maior número de downloads abertos ao mesmo tempo.</summary>
    public int MaxConcurrent { get; private set; }

    public async Task<Stream> OpenAsync(Uri serverUrl, string sha256, CancellationToken ct)
    {
        lock (_gate)
        {
            Requested.Add(sha256);
            MaxConcurrent = Math.Max(MaxConcurrent, ++_emVoo);
        }

        try
        {
            if (Delay > TimeSpan.Zero)
                await Task.Delay(Delay, ct);
        }
        finally
        {
            lock (_gate)
                _emVoo--;
        }

        return new MemoryStream(Encoding.UTF8.GetBytes($"conteudo-{sha256}"));
    }
}
