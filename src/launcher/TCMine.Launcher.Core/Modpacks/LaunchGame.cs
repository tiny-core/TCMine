using TCMine.Contracts;
using TCMine.Contracts.Identity;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Runtime;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Abre o jogo na instância ativa.
///     Junta quatro coisas que só valem juntas: a instância sabe o que rodar, o
///     Java certo tem de existir no disco, a conta tem de provar-se outra vez, e
///     só então alguém abre o processo. Cada uma falha de um jeito diferente, e a
///     razão de este caso de uso existir é que o jogador saiba QUAL delas falhou
///     — "não foi possível abrir o jogo" não diz se ele deve entrar de novo,
///     reinstalar, ou esperar a rede voltar.
/// </summary>
public sealed class LaunchGame(
    IMinecraftAuthenticator authenticator,
    IJavaLocator java,
    IGameLauncher launcher)
{
    public async Task<GameLaunchResult> HandleAsync(
        InstalledInstance instance,
        LauncherConfig config,
        LauncherSessionDto session,
        IProgress<GameLaunchProgress>? progress,
        CancellationToken ct)
    {
        var manifesto = instance.Manifest;

        // Instalada por uma build anterior a isto existir. Adivinhar a versão
        // abriria o jogo errado — ou nenhum —, e reinstalar resolve de vez.
        if (!manifesto.CanLaunch)
        {
            return GameLaunchResult.Failed(
                $"Esta instância foi instalada por uma versão antiga do launcher e não sabe qual "
                + $"Minecraft executar. Reinstale {manifesto.ModpackName} para poder jogar.");
        }

        // O token PRIMEIRO, e de propósito: é o único passo que pode exigir o
        // jogador, e descobri-lo depois de baixar cinquenta megabytes de Java
        // seria fazê-lo esperar para só então pedir que entre outra vez.
        progress?.Report(new GameLaunchProgress("Verificando a conta"));

        var conta = await authenticator.TrySilentAsync(config.AzureClientId, ct);

        if (conta.Outcome is not AuthOutcome.Success)
            return GameLaunchResult.Failed(MensagemDaConta(conta));

        progress?.Report(new GameLaunchProgress("Preparando o Java"));

        var major = JavaRequirement.ForMinecraft(manifesto.MinecraftVersion);

        string javaPath;

        try
        {
            javaPath = await java.EnsureRuntimeAsync(
                major,
                new Progress<double>(f => progress?.Report(new GameLaunchProgress("Baixando o Java", f))),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Rede fora, Adoptium fora, disco cheio. A causa real vale mais do
            // que uma mensagem nossa: é o que diz se adianta tentar de novo.
            return GameLaunchResult.Failed($"Não foi possível preparar o Java {major}. {ex.Message}");
        }

        return await launcher.LaunchAsync(
            new GameLaunchRequest
            {
                InstanceDirectory = instance.Path,
                MinecraftVersion = manifesto.MinecraftVersion!,
                Loader = manifesto.Loader!.Value,
                LoaderVersion = manifesto.LoaderVersion,
                JavaPath = javaPath,
                PlayerName = session.DisplayName,
                PlayerUuid = session.MinecraftUuid,
                AccessToken = conta.AccessToken!,
                MemoryMb = manifesto.MemoryMb
            },
            progress,
            ct);
    }

    /// <summary>
    ///     Por que a conta não serviu, na língua de quem vai agir.
    ///     O desfecho importa: sem credencial guardada pede-se para entrar, e
    ///     qualquer outra coisa é um problema que tentar de novo pode resolver.
    ///     Trocar os dois faria o jogador clicar em "tentar de novo" para sempre
    ///     quando o que faltava era fazer login.
    /// </summary>
    private static string MensagemDaConta(AuthResult conta) => conta.Outcome switch
    {
        AuthOutcome.NoStoredCredentials =>
            "A sua sessão com a Microsoft expirou. Entre novamente para jogar.",

        AuthOutcome.Cancelled => "Entrada cancelada.",

        _ => conta.Message ?? "Não foi possível verificar a sua conta Minecraft."
    };
}
