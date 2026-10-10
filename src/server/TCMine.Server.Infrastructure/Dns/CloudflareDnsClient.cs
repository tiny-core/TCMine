using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Dns;

/// <summary>
///     A API v4 da Cloudflare, no que toca a registros de DNS.
///     Toda resposta dela vem num envelope (<c>success</c>, <c>errors</c>,
///     <c>result</c>), inclusive as de erro — e é dali que sai a mensagem que o
///     admin lê. Um "403" sozinho não diz que faltou a permissão de DNS no token;
///     o texto da Cloudflare diz.
///     Sem a resiliência padrão dos outros clientes: criar registro não é
///     idempotente, e quem tenta de novo aqui é a sincronização seguinte, que
///     recalcula a diferença em vez de repetir o pedido às cegas.
/// </summary>
public sealed class CloudflareDnsClient(HttpClient http) : ICloudflareDns
{
    private const int PageSize = 100;

    /// <summary>Teto de páginas por listagem: uma resposta estranha não vira laço sem fim.</summary>
    private const int MaxPages = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<IReadOnlyList<DnsRecord>> ListByCommentPrefixAsync(
        DnsZone zone, string commentPrefix, CancellationToken ct) =>
        ListAsync(zone, $"comment.startswith={Uri.EscapeDataString(commentPrefix)}", ct);

    public async Task<IReadOnlyList<DnsRecord>> ListByNameAsync(DnsZone zone, string name, CancellationToken ct)
    {
        var records = await ListAsync(zone, $"name.exact={Uri.EscapeDataString(name)}", ct);

        // O filtro é repetido aqui de propósito: um parâmetro que a API deixe de
        // reconhecer é ignorado em silêncio, e ela devolveria a zona inteira.
        return [.. records.Where(r => string.Equals(r.Spec.Name, name, StringComparison.OrdinalIgnoreCase))];
    }

    public Task CreateAsync(DnsZone zone, DnsRecordSpec record, CancellationToken ct) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"zones/{Zone(zone)}/dns_records", zone, Body(record), ct);

    public Task ReplaceAsync(DnsZone zone, string recordId, DnsRecordSpec record, CancellationToken ct) =>
        SendAsync<JsonElement>(
            HttpMethod.Put,
            $"zones/{Zone(zone)}/dns_records/{Uri.EscapeDataString(recordId)}",
            zone,
            Body(record),
            ct);

    public Task DeleteAsync(DnsZone zone, string recordId, CancellationToken ct) =>
        SendAsync<JsonElement>(
            HttpMethod.Delete,
            $"zones/{Zone(zone)}/dns_records/{Uri.EscapeDataString(recordId)}",
            zone,
            null,
            ct);

    private async Task<IReadOnlyList<DnsRecord>> ListAsync(DnsZone zone, string filter, CancellationToken ct)
    {
        var records = new List<DnsRecord>();

        for (var page = 1; page <= MaxPages; page++)
        {
            var batch = await SendAsync<List<RecordDto>>(
                HttpMethod.Get,
                $"zones/{Zone(zone)}/dns_records?{filter}&per_page={PageSize}&page={page}",
                zone,
                null,
                ct) ?? [];

            records.AddRange(batch.Where(r => r.Id is not null).Select(ToRecord));

            if (batch.Count < PageSize)
                break;
        }

        return records;
    }

    private async Task<T?> SendAsync<T>(
        HttpMethod method, string path, DnsZone zone, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", zone.ApiToken);

        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);

        try
        {
            using var response = await http.SendAsync(request, ct);

            Envelope<T>? envelope = null;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(Json, ct);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                // Um proxy no caminho, ou uma página de erro em HTML (que nem
                // JSON declara ser, e por isso lança NotSupported em vez de
                // falhar o parse): fica o status, que é o que há.
            }

            if (!response.IsSuccessStatusCode || envelope is not { Success: true })
                throw new DnsProviderException(Describe(response.StatusCode, envelope?.Errors));

            return envelope.Result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !ct.IsCancellationRequested)
        {
            throw new DnsProviderException($"Não foi possível falar com a Cloudflare: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     A mensagem para o admin. Os dois status de credencial ganham a dica do
    ///     que conferir — são, de longe, o erro mais comum na primeira
    ///     configuração — e o texto da Cloudflare vai junto, porque é ele que diz
    ///     o que ela recusou.
    /// </summary>
    public static string Describe(HttpStatusCode status, IReadOnlyList<ErrorDto>? errors)
    {
        var detail = errors is { Count: > 0 }
            ? string.Join("; ", errors.Select(e => $"{e.Message} (código {e.Code})"))
            : $"HTTP {(int)status}";

        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "A Cloudflare recusou o token. Confira se ele tem a permissão Zone → DNS → Edit para esta "
                + $"zona e se o Zone ID é o deste domínio. {detail}",
            HttpStatusCode.NotFound =>
                $"A Cloudflare não encontrou a zona ou o registro. Confira o Zone ID. {detail}",
            _ => $"A Cloudflare respondeu com erro: {detail}"
        };
    }

    // O Zone ID é validado ao gravar (32 hexadecimais), mas vai escapado mesmo
    // assim: é texto do admin a entrar no caminho de uma URL.
    private static string Zone(DnsZone zone) => Uri.EscapeDataString(zone.ZoneId);

    /// <summary>
    ///     O corpo de um registro. TTL 1 é "automático" na Cloudflare.
    ///     <c>proxied: false</c> no A é obrigatório para o jogo: o proxy da
    ///     Cloudflare só passa HTTP, e um registro A com a nuvem laranja deixaria
    ///     o nome a apontar para um IP que não fala Minecraft.
    /// </summary>
    private static object Body(DnsRecordSpec record) =>
        string.Equals(record.Type, DnsRecordTypes.Srv, StringComparison.OrdinalIgnoreCase)
            ? new SrvBody(
                DnsRecordTypes.Srv,
                record.Name,
                1,
                record.Comment,
                new SrvData(0, 0, record.Port, record.Target))
            : new AddressBody(record.Type, record.Name, record.Target, 1, false, record.Comment);

    private static DnsRecord ToRecord(RecordDto dto) =>
        new(dto.Id!,
            new DnsRecordSpec(
                dto.Type ?? "",
                dto.Name ?? "",

                // Num SRV o alvo e a porta vêm em "data"; o "content" é só a
                // forma de texto dos mesmos campos.
                dto.Data?.Target ?? dto.Content ?? "",
                dto.Data?.Port ?? 0,
                dto.Comment ?? ""));

    private sealed record Envelope<T>(bool Success, IReadOnlyList<ErrorDto>? Errors, T? Result);

    public sealed record ErrorDto(int Code, string? Message);

    private sealed record RecordDto(
        string? Id,
        string? Type,
        string? Name,
        string? Content,
        string? Comment,
        SrvData? Data);

    private sealed record SrvData(int? Priority, int? Weight, int? Port, string? Target);

    private sealed record AddressBody(string Type, string Name, string Content, int Ttl, bool Proxied, string Comment);

    private sealed record SrvBody(string Type, string Name, int Ttl, string Comment, SrvData Data);
}
