using System.Text.Json.Serialization;

namespace TCMine.Launcher.Infrastructure.Identity;

/// <summary>
///     O formato de fio do Xbox Live e do Minecraft Services.
///     Fica em contexto próprio, separado do <c>TcMineJsonContext</c>, porque
///     nada disto é contrato do TCMine: são APIs de terceiros, que usam
///     PascalCase (Xbox) e snake_case (Minecraft) na mesma cadeia. Misturá-las
///     com os nossos DTOs obrigaria a política de nomes do nosso contexto a
///     servir a três convenções ao mesmo tempo.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(XboxAuthRequest))]
[JsonSerializable(typeof(XboxAuthResponse))]
[JsonSerializable(typeof(XstsErrorResponse))]
[JsonSerializable(typeof(MinecraftLoginWithXboxRequest))]
[JsonSerializable(typeof(MinecraftLoginWithXboxResponse))]
internal sealed partial class XboxAuthJsonContext : JsonSerializerContext;

internal sealed record XboxAuthRequest
{
    public required XboxAuthProperties Properties { get; init; }

    public required string RelyingParty { get; init; }

    public string TokenType { get; init; } = "JWT";
}

/// <summary>
///     Serve aos dois pedidos da cadeia, que partilham o envelope e diferem no
///     miolo: o do Xbox Live manda o ticket da Microsoft, o do XSTS manda o token
///     que o primeiro devolveu. Os campos que não valem para o pedido em questão
///     saem do JSON em vez de irem nulos — o Xbox Live recusa propriedade
///     desconhecida com 400, sem dizer qual.
/// </summary>
internal sealed record XboxAuthProperties
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthMethod { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SiteName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RpsTicket { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SandboxId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? UserTokens { get; init; }
}

internal sealed record XboxAuthResponse
{
    public string? Token { get; init; }

    public XboxDisplayClaims? DisplayClaims { get; init; }

    /// <summary>
    ///     O "user hash", que identifica o jogador dentro do token. Vai junto do
    ///     token no cabeçalho do Minecraft, e sem ele o login é recusado.
    /// </summary>
    public string? UserHash => DisplayClaims?.Xui?.FirstOrDefault()?.Uhs;
}

/// <summary>
///     Os dois únicos campos minúsculos de uma resposta toda em PascalCase.
///     Não é capricho do formato: sem os <c>JsonPropertyName</c> abaixo, o
///     user hash volta nulo, o login falha no último salto e o erro aponta para o
///     Minecraft — que não tem nada com isso.
/// </summary>
internal sealed record XboxDisplayClaims
{
    [JsonPropertyName("xui")]
    public XboxUserClaim[]? Xui { get; init; }
}

internal sealed record XboxUserClaim
{
    [JsonPropertyName("uhs")]
    public string? Uhs { get; init; }
}

/// <summary>
///     O corpo do 401 do XSTS. O <c>XErr</c> é a única parte útil: é ele que
///     distingue "não tem perfil do Xbox" de "conta infantil", e essas duas
///     exigem coisas diferentes do jogador.
/// </summary>
internal sealed record XstsErrorResponse
{
    public long XErr { get; init; }
}

internal sealed record MinecraftLoginWithXboxRequest
{
    [JsonPropertyName("identityToken")]
    public required string IdentityToken { get; init; }
}

internal sealed record MinecraftLoginWithXboxResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }
}
