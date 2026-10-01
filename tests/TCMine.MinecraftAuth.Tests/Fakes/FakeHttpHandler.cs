using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TCMine.MinecraftAuth.Tests.Fakes;

/// <summary>
///     Responde por URL, e guarda o que foi pedido.
///     Fake e não mock: o que estes testes precisam verificar é a CONVERSA — que
///     o ticket foi prefixado, que o token do salto anterior seguiu para o
///     seguinte, que o cabeçalho do Minecraft tem o par certo. Isso se afirma
///     sobre os corpos capturados, não sobre "o método X foi chamado".
/// </summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _respostas = new(StringComparer.Ordinal);

    /// <summary>Corpo enviado a cada URL, na ordem em que foi pedido.</summary>
    public List<(string Url, string Body)> Pedidos { get; } = [];

    public FakeHttpHandler Responde(string url, HttpStatusCode status, object? corpo = null)
    {
        _respostas[url] = () => new HttpResponseMessage(status)
        {
            Content = corpo is null
                ? new StringContent("")
                : JsonContent.Create(corpo, corpo.GetType(), options: new JsonSerializerOptions())
        };

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();

        var corpo = request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Pedidos.Add((url, corpo));

        // Sem resposta registrada é erro do teste, não cenário: um 404 silencioso
        // faria o caso passar pelo caminho de falha e parecer verde por engano.
        if (!_respostas.TryGetValue(url, out var response))
            throw new InvalidOperationException($"O teste não registrou resposta para {url}.");

        return response();
    }
}
