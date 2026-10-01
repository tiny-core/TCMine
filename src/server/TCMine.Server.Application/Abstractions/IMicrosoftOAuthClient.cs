using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Troca um código de autorização (Authorization Code + PKCE, sem client
///     secret — mesmo app público que o launcher já usa, só que chegando ao
///     código por um redirect de navegador em vez de MSAL) por um token
///     Microsoft, com o escopo <c>XboxLive.signin</c> que a cadeia do Xbox
///     exige, mais o <c>oid</c> e o nome de quem acabou de provar a conta.
/// </summary>
public interface IMicrosoftOAuthClient
{
    Task<Result<MicrosoftIdentity>> ExchangeCodeAsync(
        string clientId, string code, string redirectUri, string codeVerifier, CancellationToken ct);
}

/// <summary>
///     O que sobra do token Microsoft depois de usado: a identidade estável
///     (<see cref="ObjectId" />, a claim "oid" do id_token) e o token de
///     acesso, que ainda serve para a cadeia Xbox Live → XSTS → Minecraft.
///     O token não é guardado por quem recebe isto — vale só a troca que o
///     chamou.
/// </summary>
public sealed record MicrosoftIdentity(string ObjectId, string DisplayName, string AccessToken);
