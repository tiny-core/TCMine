using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Web.Tests.Infrastructure;

/// <summary>
///     Substitui a conversa real com a Microsoft nos testes de integração: a
///     identidade devolvida é derivada do próprio "code", sem rede nenhuma — o
///     mesmo code sempre vira o mesmo oid, e codes diferentes viram contas
///     diferentes. É o único trecho do par /start → /callback que os testes não
///     exercitam de verdade (ver <see cref="AutenticacaoDeTeste" />).
/// </summary>
internal sealed class FakeMicrosoftOAuthClient : IMicrosoftOAuthClient
{
    public Task<Result<MicrosoftIdentity>> ExchangeCodeAsync(
        string clientId, string code, string redirectUri, string codeVerifier, CancellationToken ct) =>
        Task.FromResult(Result<MicrosoftIdentity>.Success(
            new MicrosoftIdentity($"oid-{code}", $"Jogador {code}", $"ms-token-{code}")));
}

/// <summary>
///     IP público fixo, sem rede. As listas de servidores, o formulário e a aba
///     Rede consultam o provedor de verdade — sem este registro, renderizar
///     essas páginas num teste sairia para a Cloudflare.
///     O valor é da faixa reservada a documentação (RFC 5737).
/// </summary>
internal sealed class FakePublicAddressProvider : IPublicAddressProvider
{
    public const string Address = "203.0.113.7";

    public Task<string?> GetAsync(CancellationToken ct) => Task.FromResult<string?>(Address);
}

/// <summary>
///     Sem Minecraft por padrão: a maioria dos testes que bootstrapam um admin
///     não tem nada a ver com a cadeia Xbox Live → XSTS, e deixá-la real faria a
///     suíte depender da rede. Testes que precisam de um admin COM Minecraft
///     substituem este registro pelo seu próprio fake, via
///     <see cref="TcMineAppFactory.Servicos" />.
/// </summary>
internal sealed class FakeMinecraftTokenExchange : IMinecraftTokenExchange
{
    public Task<Result<string>> ExchangeAsync(string microsoftAccessToken, CancellationToken ct) =>
        Task.FromResult(Result<string>.Fail("sem Minecraft neste teste"));
}
