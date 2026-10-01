namespace TCMine.MinecraftAuth;

/// <summary>
///     O token do Minecraft, ou o motivo de não haver um.
///     Mesma forma do <c>AuthResult</c> do launcher (êxito, ou motivo), mas sem os
///     desfechos que só fazem sentido do lado de lá — "nada guardado",
///     "o jogador fechou a janela" — porque esta troca nunca lida com
///     credencial guardada nem com janela nenhuma: ela só recebe um token
///     Microsoft já em mãos e devolve um do Minecraft, ou a razão de não ter
///     conseguido.
/// </summary>
public sealed record MinecraftTokenResult(
    MinecraftTokenOutcome Outcome,
    string? AccessToken,
    string? Message)
{
    public static MinecraftTokenResult Success(string accessToken) =>
        new(MinecraftTokenOutcome.Success, accessToken, null);

    public static MinecraftTokenResult Failed(string message) =>
        new(MinecraftTokenOutcome.Failed, null, message);
}

public enum MinecraftTokenOutcome
{
    Success,
    Failed
}
