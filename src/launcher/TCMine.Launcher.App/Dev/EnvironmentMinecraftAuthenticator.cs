#if DEBUG
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.App.Dev;

/// <summary>
///     Atalho de desenvolvimento: usa um token REAL do Minecraft, lido de
///     <c>TCMINE_DEV_MINECRAFT_TOKEN</c>.
///     Não é um bypass, e a diferença importa. O servidor continua verificando o
///     token com a Mojang; um valor inventado é recusado lá, como qualquer
///     outro. O que isto dispensa é refazer o fluxo interativo a cada execução
///     enquanto o MSAL não existe — nada mais.
///     Compilado apenas em Debug: numa build de release este arquivo não existe,
///     então não há como ficar ligado por engano em produção.
/// </summary>
internal sealed class EnvironmentMinecraftAuthenticator : IMinecraftAuthenticator
{
    private const string Variavel = "TCMINE_DEV_MINECRAFT_TOKEN";

    private static string? Token => Environment.GetEnvironmentVariable(Variavel);

    /// <summary>
    ///     Silencioso quando não há token: o arranque não deve reclamar de uma
    ///     variável que só o desenvolvedor conhece.
    /// </summary>
    public Task<MinecraftAuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
        Task.FromResult(Token is { Length: > 0 } token
            ? MinecraftAuthResult.Success(token)
            : MinecraftAuthResult.NoStoredCredentials());

    public Task<MinecraftAuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
        Task.FromResult(Token is { Length: > 0 } token
            ? MinecraftAuthResult.Success(token)
            : MinecraftAuthResult.Unavailable(
                $"Build de desenvolvimento sem {Variavel}. Defina a variável com um token "
                + "de acesso do Minecraft para entrar, ou aguarde a versão com login da Microsoft."));

    public Task SignOutAsync(CancellationToken ct) => Task.CompletedTask;
}
#endif
