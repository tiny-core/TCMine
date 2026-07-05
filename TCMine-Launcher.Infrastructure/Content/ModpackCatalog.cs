using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TCMine_Application.Contracts;
using TCMine_Application.Launcher;
using TCMine_Launcher.Infrastructure.Configuration;

namespace TCMine_Launcher.Infrastructure.Content;

/// <summary>Leitura do catálogo de modpacks do servidor. Implementa <see cref="IModpackCatalog"/>.</summary>
public sealed class ModpackCatalog(ServerConfig config) : IModpackCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = new(CreateHandler()) { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<IReadOnlyList<ModpackSummaryDto>> GetModpacksAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<ModpackSummaryDto>>(config.Resolve("/api/modpacks"), Json, ct) ?? [];

    public async Task<ModpackManifestDto?> GetManifestAsync(Guid modpackId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<ModpackManifestDto>(config.Resolve($"/api/modpacks/{modpackId}"), Json, ct);

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(config.Resolve("/api/modpacks"), ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static HttpMessageHandler CreateHandler()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
        return handler;
    }
}
