using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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
        var v = options.Value.ApiVersion;
        _http = factory.Create();
        _prefix = string.IsNullOrWhiteSpace(v) ? "" : $"/{v}";
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

    /// <summary>
    ///     Retrato único de consumo (stream=false). Devolve null quando o
    ///     container não existe ou está parado.
    /// </summary>
    internal async Task<DockerStatsResponse?> GetStatsAsync(string nameOrId, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"{_prefix}/containers/{nameOrId}/stats?stream=false", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<DockerStatsResponse>(ct);
    }

    /// <summary>
    ///     Segue o console do container, linha a linha.
    ///     O stream do Docker é MULTIPLEXADO quando o container não tem TTY (o
    ///     nosso não tem): cada quadro traz 8 bytes de cabeçalho — 1 byte de
    ///     canal (1=stdout, 2=stderr), 3 de padding e 4 com o tamanho do corpo em
    ///     big-endian. Ler o corpo como texto direto entregaria lixo binário no
    ///     meio das linhas.
    /// </summary>
    internal async IAsyncEnumerable<string> StreamLogsAsync(
        string nameOrId, int tail, [EnumeratorCancellation] CancellationToken ct)
    {
        var url = $"{_prefix}/containers/{nameOrId}/logs?follow=1&stdout=1&stderr=1&tail={tail}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
            yield break;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        var header = new byte[8];
        var pendente = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            if (!await FillAsync(stream, header, ct))
                break;

            var tamanho = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4));
            if (tamanho <= 0)
                continue;

            var corpo = new byte[tamanho];
            if (!await FillAsync(stream, corpo, ct))
                break;

            // Um quadro pode trazer meia linha ou três; o buffer junta os
            // pedaços e só entrega o que já terminou em quebra de linha.
            pendente.Append(Encoding.UTF8.GetString(corpo));

            while (true)
            {
                var texto = pendente.ToString();
                var quebra = texto.IndexOf('\n', StringComparison.Ordinal);
                if (quebra < 0)
                    break;

                yield return texto[..quebra].TrimEnd('\r');
                pendente.Remove(0, quebra + 1);
            }
        }
    }

    /// <summary>Lê exatamente o tamanho do buffer. False quando o stream acabou.</summary>
    private static async Task<bool> FillAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var lidos = 0;
        while (lidos < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(lidos), ct);
            if (n is 0)
                return false;

            lidos += n;
        }

        return true;
    }

    /// <summary>
    ///     Roda um comando DENTRO do container e devolve a saída.
    ///     Preferido a abrir a porta do RCON no host: o segredo nunca sai do
    ///     container, nada fica escutando na rede, e não é preciso recriar
    ///     containers já existentes para ganhar a funcionalidade.
    ///     Tty=true para a saída vir crua — sem ele o Docker multiplexa stdout e
    ///     stderr com um cabeçalho de 8 bytes por quadro, que teríamos de
    ///     desembrulhar à mão.
    /// </summary>
    internal async Task<string> ExecAsync(string nameOrId, IReadOnlyList<string> command, CancellationToken ct)
    {
        var create = await _http.PostAsJsonAsync(
            $"{_prefix}/containers/{nameOrId}/exec",
            new ExecCreateRequest { Cmd = command },
            ct);

        create.EnsureSuccessStatusCode();

        var created = await create.Content.ReadFromJsonAsync<CreateContainerResponse>(ct);
        if (created?.Id is not { Length: > 0 } execId)
            return "";

        using var start = await _http.PostAsJsonAsync(
            $"{_prefix}/exec/{execId}/start",
            new ExecStartRequest(),
            ct);

        start.EnsureSuccessStatusCode();
        return await start.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Cria um container a partir de uma spec. Devolve o ID.</summary>
    public async Task<string> CreateContainerAsync(string name, CreateContainerRequest spec, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(
            $"{_prefix}/containers/create?name={Uri.EscapeDataString(name)}", spec, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateContainerResponse>(ct);
        return created!.Id;
    }

    public async Task StartContainerAsync(string id, CancellationToken ct)
    {
        var response = await _http.PostAsync($"{_prefix}/containers/{id}/start", null, ct);
        // 304 = já corria; não é erro.
        if (response.StatusCode is not HttpStatusCode.NotModified)
            response.EnsureSuccessStatusCode();
    }

    /// <summary>Parada graciosa: SIGTERM, espera t segundos, depois SIGKILL.</summary>
    public async Task StopContainerAsync(string id, int timeoutSeconds, CancellationToken ct)
    {
        var response = await _http.PostAsync($"{_prefix}/containers/{id}/stop?t={timeoutSeconds}", null, ct);
        if (response.StatusCode is not HttpStatusCode.NotModified)
            response.EnsureSuccessStatusCode();
    }

    /// <summary>
    ///     Remove um container pelo nome, se existir. Idempotente — silêncio se não
    ///     houver nenhum. Usado antes de criar, para o nome nunca colidir com um
    ///     resto de tentativa anterior.
    /// </summary>
    public async Task RemoveContainerByNameAsync(string name, CancellationToken ct)
    {
        // O filtro do Docker casa por substring, então confirmamos o nome exato
        // ("/name", como o daemon devolve) antes de remover.
        var filters = Uri.EscapeDataString($"{{\"name\":[\"{name}\"]}}");
        var found = await _http.GetFromJsonAsync<List<DockerContainer>>(
            $"{_prefix}/containers/json?all=true&filters={filters}", ct);

        var match = found?.FirstOrDefault(c => c.Names.Any(n => n.TrimStart('/') == name));
        if (match is not null)
            await RemoveContainerAsync(match.Id, true, ct);
    }

    /// <summary>Estado atual do container, ou null se não existe (404).</summary>
    public async Task<ContainerInspect?> InspectContainerAsync(string id, CancellationToken ct)
    {
        var response = await _http.GetAsync($"{_prefix}/containers/{id}/json", ct);
        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ContainerInspect>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
    }

    public async Task RemoveContainerAsync(string id, bool force, CancellationToken ct)
    {
        var response = await _http.DeleteAsync(
            $"{_prefix}/containers/{id}?force={force.ToString().ToLowerInvariant()}", ct);
        if (response.StatusCode is not HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    /// <summary>
    ///     Puxa uma imagem do registry. A rota devolve progresso em stream (uma
    ///     linha JSON por evento); drenamos até ao fim, que é quando o pull acaba.
    /// </summary>
    public async Task PullImageAsync(string image, CancellationToken ct)
    {
        var response = await _http.PostAsync(
            $"{_prefix}/images/create?fromImage={Uri.EscapeDataString(image)}", null, ct);
        response.EnsureSuccessStatusCode();

        // Consumir o stream até ao fim é o que "espera o pull terminar".
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is not null)
        {
            // Poderíamos parsear o progresso para a UI; por ora só drenamos.
        }
    }
}

internal sealed record ExecCreateRequest
{
    [JsonPropertyName("AttachStdout")] public bool AttachStdout { get; init; } = true;
    [JsonPropertyName("AttachStderr")] public bool AttachStderr { get; init; } = true;
    [JsonPropertyName("Tty")] public bool Tty { get; init; } = true;
    [JsonPropertyName("Cmd")] public required IReadOnlyList<string> Cmd { get; init; }
}

internal sealed record ExecStartRequest
{
    [JsonPropertyName("Detach")] public bool Detach { get; init; }
    [JsonPropertyName("Tty")] public bool Tty { get; init; } = true;
}

public sealed record DockerContainer
{
    [JsonPropertyName("Id")] public string Id { get; init; } = "";
    [JsonPropertyName("Names")] public IReadOnlyList<string> Names { get; init; } = [];
    [JsonPropertyName("Image")] public string Image { get; init; } = "";
    [JsonPropertyName("State")] public string State { get; init; } = "";
    [JsonPropertyName("Status")] public string Status { get; init; } = "";
}

public sealed record CreateContainerRequest
{
    [JsonPropertyName("Image")] public required string Image { get; init; }
    [JsonPropertyName("Env")] public required IReadOnlyList<string> Env { get; init; }
    [JsonPropertyName("HostConfig")] public required HostConfig HostConfig { get; init; }
    [JsonPropertyName("ExposedPorts")] public Dictionary<string, object>? ExposedPorts { get; init; }
    [JsonPropertyName("Labels")] public Dictionary<string, string>? Labels { get; init; }
}

public sealed record HostConfig
{
    [JsonPropertyName("Binds")] public required IReadOnlyList<string> Binds { get; init; }
    [JsonPropertyName("PortBindings")] public Dictionary<string, PortBinding[]>? PortBindings { get; init; }
    [JsonPropertyName("RestartPolicy")] public RestartPolicy? RestartPolicy { get; init; }
    [JsonPropertyName("Memory")] public long Memory { get; init; }
}

public sealed record PortBinding
{
    [JsonPropertyName("HostPort")] public required string HostPort { get; init; }
}

public sealed record RestartPolicy
{
    [JsonPropertyName("Name")] public required string Name { get; init; } // "unless-stopped"
}

public sealed record CreateContainerResponse
{
    [JsonPropertyName("Id")] public string Id { get; init; } = "";
}

public sealed record ContainerInspect
{
    [JsonPropertyName("Id")] public string Id { get; init; } = "";
    [JsonPropertyName("State")] public ContainerState State { get; init; } = new();
}

public sealed record ContainerState
{
    [JsonPropertyName("Status")] public string Status { get; init; } = ""; // running, exited, created…
    [JsonPropertyName("Running")] public bool Running { get; init; }
    [JsonPropertyName("ExitCode")] public int ExitCode { get; init; }
}
