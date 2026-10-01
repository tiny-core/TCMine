using TCMine.Launcher.Core.Abstractions;
using TCMine.MinecraftAuth;

namespace TCMine.Launcher.Infrastructure.Identity;

/// <summary>
///     Da conta Microsoft ao token do Minecraft: o degrau Microsoft é
///     <see cref="IMicrosoftTokenProvider" /> (MSAL, Windows); os três saltos
///     seguintes (Xbox Live, XSTS, Minecraft Services) são o
///     <see cref="MinecraftTokenExchange" /> compartilhado — o painel web troca
///     pelo MESMO serviço depois de chegar ao token Microsoft por um caminho
///     diferente (redirect de navegador, não MSAL). A divisão importa no dia
///     do port para Linux: escreve-se um provedor de token novo, não uma
///     autenticação inteira.
/// </summary>
public sealed class MinecraftAuthenticator(
    IMicrosoftTokenProvider microsoft,
    MinecraftTokenExchange exchange) : IMinecraftAuthenticator
{
    public async Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
        await ContinueAsync(await microsoft.TrySilentAsync(azureClientId, ct), ct);

    public async Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
        await ContinueAsync(await microsoft.SignInAsync(azureClientId, ct), ct);

    public Task SignOutAsync(CancellationToken ct) => microsoft.SignOutAsync(ct);

    /// <summary>
    ///     Só há o que fazer quando a Microsoft disse sim. Todo outro desfecho é
    ///     devolvido intacto de propósito: "não havia credencial guardada" e "o
    ///     jogador fechou a janela" já são respostas completas, e reembrulhá-las
    ///     como falha genérica apagaria a diferença que a tela usa para decidir
    ///     entre calar-se e mostrar um erro.
    /// </summary>
    private async Task<AuthResult> ContinueAsync(AuthResult microsoftToken, CancellationToken ct)
    {
        if (microsoftToken.Outcome is not AuthOutcome.Success)
            return microsoftToken;

        var minecraft = await exchange.ExchangeAsync(microsoftToken.AccessToken!, ct);

        return minecraft.Outcome is MinecraftTokenOutcome.Success
            ? AuthResult.Success(minecraft.AccessToken!)
            : AuthResult.Failed(minecraft.Message!);
    }
}
