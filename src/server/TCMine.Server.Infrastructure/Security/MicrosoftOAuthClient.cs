using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Infrastructure.Security;

/// <summary>
///     Troca um código de autorização pelo token Microsoft, via Authorization
///     Code + PKCE sem client secret — o mesmo app público que o launcher já
///     usa (MSAL, audiência "contas pessoais"), só que aqui chegamos ao código
///     por um redirect de navegador comum, não pelo broker do Windows. Por
///     isso é um POST simples ao endpoint de token, não MSAL.NET: a biblioteca
///     é feita para apps com sessão de usuário local (desktop/mobile) ou com
///     client secret (confidential), nenhum dos dois é este caso.
/// </summary>
public sealed partial class MicrosoftOAuthClient(
    HttpClient http,
    ILogger<MicrosoftOAuthClient> logger) : IMicrosoftOAuthClient
{
    // Mesma audiência do launcher (AadAuthorityAudience.PersonalMicrosoftAccount
    // no MSAL): só contas pessoais, que são as únicas com Xbox Live/Minecraft.
    private static readonly Uri TokenUrl =
        new("https://login.microsoftonline.com/consumers/oauth2/v2.0/token");

    public async Task<Result<MicrosoftIdentity>> ExchangeCodeAsync(
        string clientId, string code, string redirectUri, string codeVerifier, CancellationToken ct)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,

            // openid: sem ele não vem id_token, e é dele que tiramos oid e nome.
            // XboxLive.signin: o escopo que a cadeia Xbox Live exige no passo
            // seguinte — pedi-lo aqui é o que faz o token devolvido já servir
            // para ela, sem uma segunda ida à Microsoft.
            ["scope"] = "openid profile XboxLive.signin offline_access"
        });

        using var response = await http.PostAsync(TokenUrl, body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadAsStringAsync(ct);
            LogTokenExchangeFailed((int)response.StatusCode);

            // O corpo de erro da Microsoft é JSON com "error"/"error_description",
            // útil em log — mas não para o jogador, que não vai entender
            // "invalid_grant". Essa tradução fica para quem chama.
            return Result<MicrosoftIdentity>.Fail(
                $"A Microsoft recusou a troca do código (HTTP {(int)response.StatusCode}). {Resumo(problem)}");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);

        if (payload?.AccessToken is not { Length: > 0 } accessToken
            || payload.IdToken is not { Length: > 0 } idToken)
            return Result<MicrosoftIdentity>.Fail("A Microsoft respondeu sem token de acesso ou de identidade.");

        var claims = DecodeIdTokenClaims(idToken);

        if (claims?.ObjectId is not { Length: > 0 } objectId)
            return Result<MicrosoftIdentity>.Fail("O token de identidade da Microsoft veio sem o oid.");

        return Result<MicrosoftIdentity>.Success(
            new MicrosoftIdentity(objectId, claims.Name ?? "Jogador", accessToken));
    }

    /// <summary>
    ///     Lê os claims do id_token sem validar assinatura. Não é descuido: o
    ///     token chegou por um canal HTTPS que NÓS abrimos diretamente com a
    ///     Microsoft (a troca do código, aqui em cima) — diferente de um token
    ///     que um cliente apresentasse por fora, que precisaria de validação
    ///     completa, este é confiável pela origem do pedido, não pela
    ///     assinatura.
    /// </summary>
    private static IdTokenClaims? DecodeIdTokenClaims(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length != 3)
            return null;

        var payload = Base64UrlDecode(parts[1]);
        return JsonSerializer.Deserialize<IdTokenClaims>(payload);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private static string Resumo(string problem) =>
        problem.Length > 200 ? problem[..200] : problem;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Troca de código Microsoft falhou com HTTP {Status}.")]
    private partial void LogTokenExchangeFailed(int status);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")]
        string? AccessToken,
        [property: JsonPropertyName("id_token")]
        string? IdToken);

    private sealed record IdTokenClaims(
        [property: JsonPropertyName("oid")] string? ObjectId,
        [property: JsonPropertyName("name")] string? Name);
}
