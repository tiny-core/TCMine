using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TCMine_Application.Contracts;

namespace TCMine_Launcher.Services;

/// <summary>
/// Cliente HTTP do launcher contra o TCMine Server (catálogo de modpacks). Reusa os DTOs
/// <c>record</c> do core (<c>TCMine-Application</c>) — o mesmo contrato dos dois lados, sem duplicação.
/// O login é tratado pelo <see cref="AuthService"/> (MSAL no cliente), não passa por aqui.
/// </summary>
public sealed class ApiClient(ServerConfig config)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = new(CreateHandler()) { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<IReadOnlyList<ModpackSummaryDto>> GetModpacksAsync(CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<List<ModpackSummaryDto>>(
            config.Resolve("/api/modpacks"), Json, ct) ?? [];
    }

    public async Task<ModpackManifestDto?> GetManifestAsync(Guid id, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<ModpackManifestDto>(
            config.Resolve($"/api/modpacks/{id}"), Json, ct);
    }

    /// <summary>Verifica se o servidor está alcançável (indicador da barra de estado).</summary>
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

    // Em dev o servidor usa um cert self-signed (localhost) — aceita-o SÓ em DEBUG.
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
