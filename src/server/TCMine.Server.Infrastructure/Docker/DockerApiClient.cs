using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace TCMine.Server.Infrastructure.Docker;

/// <summary>
///     Cliente REST do daemon do Docker. Cada método é uma rota da Engine API. O
///     prefixo de versão (/v1.45) evita quebrar quando o daemon avança.
/// </summary>
public sealed class DockerApiClient
{
    private readonly HttpClient _http;
    private readonly string _prefix;

    public DockerApiClient(DockerHttpClientFactory factory, IOptions<DockerOptions> options)
    {
        _http = factory.Create();
        _prefix = $"/{options.Value.ApiVersion}";
    }

    /// <summary>Testa a conexão com o daemon. Devolve true se responder "OK".</summary>
    public async Task<bool> PingAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"{_prefix}/_ping", ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>Lista containers (all=true inclui os parados).</summary>
    public async Task<IReadOnlyList<DockerContainer>> ListContainersAsync(bool all, CancellationToken ct)
    {
        var result = await _http.GetFromJsonAsync<List<DockerContainer>>(
            $"{_prefix}/containers/json?all={all.ToString().ToLowerInvariant()}", ct);
        return result ?? [];
    }
}

public sealed record DockerContainer
{
    [JsonPropertyName("Id")] public string Id { get; init; } = "";
    [JsonPropertyName("Names")] public IReadOnlyList<string> Names { get; init; } = [];
    [JsonPropertyName("Image")] public string Image { get; init; } = "";
    [JsonPropertyName("State")] public string State { get; init; } = "";
    [JsonPropertyName("Status")] public string Status { get; init; } = "";
}