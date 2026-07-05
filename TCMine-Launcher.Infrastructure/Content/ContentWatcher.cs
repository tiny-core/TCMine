using System.Net.Http;
using TCMine_Application.Launcher;
using TCMine_Launcher.Infrastructure.Configuration;
using TCMine_Launcher.Infrastructure.Networking;

namespace TCMine_Launcher.Infrastructure.Content;

/// <summary>
/// Consome o SSE <c>/events</c> do servidor: fixa a versão inicial como baseline e dispara
/// <see cref="ContentChanged"/> sempre que recebe uma versão diferente (em stream ou após reconectar).
/// Reconecta com backoff. Implementa <see cref="IContentWatcher"/>.
/// </summary>
public sealed class ContentWatcher(ServerConfig config) : IContentWatcher
{
    private readonly HttpClient _http = HttpClientProvider.Shared;
    private CancellationTokenSource? _cts;

    public event Action? ContentChanged;
    public event Action<bool>? ConnectionChanged;

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = LoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        long? known = null; // mantido entre reconexões para detetar mudanças durante a queda

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, config.Resolve("/events"));
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                ConnectionChanged?.Invoke(true);

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);

                while (await reader.ReadLineAsync(ct) is { } line)
                {
                    // Ignora keep-alives (": …") e linhas vazias; só interessa "data: <versão>".
                    if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                    if (!long.TryParse(line["data:".Length..].Trim(), out var version)) continue;

                    if (known is null) known = version;            // baseline
                    else if (version != known)
                    {
                        known = version;
                        ContentChanged?.Invoke();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                ConnectionChanged?.Invoke(false);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } // backoff antes de reconectar
            catch (OperationCanceledException) { break; }
        }
    }
}
