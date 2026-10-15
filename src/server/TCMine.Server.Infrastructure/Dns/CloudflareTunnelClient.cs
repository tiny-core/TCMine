using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Dns;

/// <summary>
///     A configuração de um túnel da Cloudflare (<c>cfd_tunnel/{id}/configurations</c>).
///     Trabalha com o JSON solto (<see cref="JsonNode" />), e não com tipos
///     nossos, porque a gravação troca a configuração INTEIRA: um campo que a
///     Cloudflare acrescente amanhã, ou um ajuste do admin numa entrada dele
///     (tempo limite, TLS), precisa voltar exatamente como veio. Um tipo nosso
///     descartaria em silêncio tudo o que não conhece.
/// </summary>
public sealed class CloudflareTunnelClient(HttpClient http) : ICloudflareTunnel
{
    public async Task<TunnelIngress> GetIngressAsync(TunnelAccount account, CancellationToken ct)
    {
        var result = await SendAsync(HttpMethod.Get, account, null, ct);

        // "cloudflare" = configurada pelo painel (a API manda); "local" = por
        // arquivo na máquina do cloudflared.
        var remote = string.Equals(
            result?["source"]?.GetValue<string>(), "cloudflare", StringComparison.OrdinalIgnoreCase);

        var config = result?["config"] as JsonObject ?? new JsonObject();
        var rules = new List<TunnelIngressRule>();

        if (config["ingress"] is JsonArray ingress)
        {
            foreach (var node in ingress.OfType<JsonObject>())
            {
                rules.Add(new TunnelIngressRule(
                    node["hostname"]?.GetValue<string>(),
                    node["service"]?.GetValue<string>() ?? "",
                    node["path"]?.GetValue<string>(),
                    node.ToJsonString()));
            }
        }

        return new TunnelIngress(rules, remote, config.ToJsonString());
    }

    public async Task PutIngressAsync(TunnelAccount account, TunnelIngress ingress, CancellationToken ct)
    {
        // Parte da configuração lida, para o que não é ingress (originRequest
        // geral, warp-routing) voltar intacto.
        var config = (ingress.Raw is { } raw ? JsonNode.Parse(raw) as JsonObject : null) ?? new JsonObject();

        var rules = new JsonArray();
        foreach (var rule in ingress.Rules)
        {
            // A regra lida volta como veio; a nova leva só o essencial.
            var node = rule.Raw is { } original
                ? JsonNode.Parse(original)!.AsObject()
                : new JsonObject();

            if (rule.Raw is null)
            {
                if (rule.Hostname is { } host)
                    node["hostname"] = host;
                node["service"] = rule.Service;
            }

            rules.Add(node);
        }

        config["ingress"] = rules;

        await SendAsync(HttpMethod.Put, account, new JsonObject { ["config"] = config }, ct);
    }

    private async Task<JsonNode?> SendAsync(
        HttpMethod method, TunnelAccount account, JsonObject? body, CancellationToken ct)
    {
        var path = $"accounts/{Uri.EscapeDataString(account.AccountId)}/cfd_tunnel/"
                   + $"{Uri.EscapeDataString(account.TunnelId)}/configurations";

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.ApiToken);

        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        try
        {
            using var response = await http.SendAsync(request, ct);

            JsonNode? envelope = null;
            try
            {
                envelope = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            }
            catch (JsonException)
            {
                // Página de erro em HTML no caminho: fica o status.
            }

            var success = envelope?["success"]?.GetValue<bool>() is true;
            if (!response.IsSuccessStatusCode || !success)
                throw new DnsProviderException(Describe(response.StatusCode, envelope?["errors"] as JsonArray));

            return envelope!["result"];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !ct.IsCancellationRequested)
        {
            throw new DnsProviderException($"Não foi possível falar com a Cloudflare: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Como no DNS: os erros de credencial ganham o que conferir, porque o
    ///     token deste recurso é o mesmo da zona com uma permissão a mais — a que
    ///     mais se esquece.
    /// </summary>
    public static string Describe(HttpStatusCode status, JsonArray? errors)
    {
        var detail = errors is { Count: > 0 }
            ? string.Join("; ", errors.OfType<JsonObject>()
                .Select(e => $"{e["message"]?.GetValue<string>()} (código {e["code"]})"))
            : $"HTTP {(int)status}";

        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "A Cloudflare recusou o token para o túnel. Confira se ele tem também a permissão "
                + $"Conta → Cloudflare Tunnel → Editar, e se o Account ID é o desta conta. {detail}",
            HttpStatusCode.NotFound =>
                $"A Cloudflare não encontrou o túnel. Confira o Account ID e o ID do túnel. {detail}",
            _ => $"A Cloudflare respondeu com erro no túnel: {detail}"
        };
    }
}
