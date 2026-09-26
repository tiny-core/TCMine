using TCMine.Contracts;
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
    IPlayerProfileSource profiles,
    IPlayerProfileCache perfilGuardado,
    IJavaLocator java,
    IJavaRequirementSource javaRequirement,
    IGameLauncher launcher,
    GameSession game)
{
    public async Task<GameLaunchResult> HandleAsync(
        InstalledInstance instance,
        LauncherConfig config,
        IProgress<GameLaunchProgress>? progress,
        CancellationToken ct)
    {
        var manifest = instance.Manifest;

        // Antes de tudo, e sem custo: duas cópias na mesma pasta escrevem o mesmo
        // mundo ao mesmo tempo e corrompem-no. O botão desabilitado já evita o
        // clique duplo, mas não evita voltar à tela e clicar outra vez.
        if (game.IsRunning)
            return GameLaunchResult.Failed("O jogo já está aberto.");

        // Instalada por uma build anterior a isto existir. Adivinhar a versão
        // abriria o jogo errado — ou nenhum —, e reinstalar resolve de vez.
        if (!manifest.CanLaunch)
        {
            return GameLaunchResult.Failed(
                $"Esta instância foi instalada por uma versão antiga do launcher e não sabe qual "
                + $"Minecraft executar. Reinstale {manifest.ModpackName} para poder jogar.");
        }

        // A conta PRIMEIRO, e de propósito: é o único passo que pode exigir o
        // jogador, e descobri-lo depois de baixar cinquenta megabytes de Java
        // seria fazê-lo esperar para só então pedir que entre outra vez.
        progress?.Report(new GameLaunchProgress("Verificando a conta"));

        var quem = await IdentifyAsync(config, ct);

        if (quem.Erro is not null)
            return GameLaunchResult.Failed(quem.Erro);

        progress?.Report(new GameLaunchProgress("Preparando o Java"));

        // A versão diz qual Java quer; o palpite é o plano B. Nesta ordem porque
        // já custou: a regra escrita à mão não entendeu "26.2" — o Minecraft
        // trocou de esquema de versão —, devolveu um Java antigo, e o jogo morreu
        // com "Could not create the Java Virtual Machine" por causa de uma flag
        // que aquele Java não conhecia.
        var major = await javaRequirement.GetRequiredJavaAsync(manifest.MinecraftVersion!, ct)
                    ?? JavaRequirement.ForMinecraft(manifest.MinecraftVersion);

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

        var result = await launcher.LaunchAsync(
            new GameLaunchRequest
            {
                InstanceDirectory = instance.Path,
                MinecraftVersion = manifest.MinecraftVersion!,
                Loader = manifest.Loader!.Value,
                LoaderVersion = manifest.LoaderVersion,
                JavaPath = javaPath,
                PlayerName = quem.Profile!.Name,
                PlayerUuid = quem.Profile.Uuid,
                AccessToken = quem.AccessToken,
                MemoryMb = manifest.MemoryMb
            },
            progress,
            ct);

        if (!result.Started || result.Process is null)
            return result;

        // Perdeu a corrida com outro arranque: o processo que acabou de abrir não
        // é de ninguém, e deixá-lo correr daria as duas cópias que o guard acima
        // existe para impedir.
        if (!game.Attach(instance, result.Process))
        {
            result.Process.Kill();
            result.Process.Dispose();

            return GameLaunchResult.Failed("O jogo já está aberto.");
        }

        return result;
    }

    /// <summary>
    ///     Quem vai jogar, e com que prova.
    ///     Repare no que NÃO aparece aqui: o servidor TCMine. A identidade que o
    ///     jogo exige é a do perfil do Minecraft, e o nosso servidor apenas a
    ///     repassava — por isso, até agora, tê-lo fora do ar impedia abrir o jogo
    ///     mesmo com internet e com a Microsoft a responder.
    ///     Três desfechos, por ordem de preferência: token e perfil vivos (joga
    ///     online); token vivo e perfil só em cache (raro, mas joga online na
    ///     mesma); e nem token nem rede, com perfil guardado — abre offline.
    /// </summary>
    private async Task<Identity> IdentifyAsync(LauncherConfig config, CancellationToken ct)
    {
        var account = await authenticator.TrySilentAsync(config.AzureClientId, ct);

        // Fechar a janela do login é uma decisão do jogador, não uma falha de
        // rede: cair no modo offline aqui seria ignorar o que ele acabou de fazer.
        if (account.Outcome is AuthOutcome.Cancelled)
            return new Identity(Erro: "Entrada cancelada.");

        if (account.Outcome is AuthOutcome.Success && account.AccessToken is { } token)
        {
            if (await profiles.GetAsync(token, ct) is { } vivo)
            {
                // Guardado só quando veio do Minecraft: gravar o do cache de volta
                // seria reescrever o mesmo ficheiro a cada abertura sem ganho.
                await perfilGuardado.WriteAsync(vivo, ct);

                return new Identity(vivo, token);
            }

            // Token bom e perfil inalcançável: continua a dar para jogar online,
            // e recusar por causa de um nome que já sabemos seria perder a
            // partida por um detalhe cosmético.
            return await perfilGuardado.ReadAsync(ct) is { } conhecido
                ? new Identity(conhecido, token)
                : new Identity(Erro:
                    "A sua conta Microsoft respondeu, mas não foi possível obter o perfil do "
                    + "Minecraft. Confirme que esta conta tem o jogo.");
        }

        // Sem conta: só resta o que ficou da última vez.
        if (await perfilGuardado.ReadAsync(ct) is { } guardado)
            return new Identity(guardado, AccessToken: null);

        return new Identity(Erro: account.Outcome is AuthOutcome.NoStoredCredentials
            ? "Entre com a sua conta Microsoft pelo menos uma vez para poder jogar."
            : account.Message ?? "Não foi possível verificar a sua conta Minecraft.");
    }

    /// <summary>
    ///     Quem joga. <c>AccessToken</c> nulo com perfil presente é o modo
    ///     offline; <c>Erro</c> preenchido é o fim da linha.
    /// </summary>
    private sealed record Identity(
        PlayerProfile? Profile = null,
        string? AccessToken = null,
        string? Erro = null);
}
