using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Game;

/// <summary>
///     O que responde enquanto o motor de arranque do jogo não existe.
///     Não é um stub vazio: quando esta classe é chamada, TUDO o que vem antes já
///     aconteceu de verdade — a instância foi validada, a conta provou-se outra
///     vez à Microsoft, e o JRE certo está no disco. O que falta é o último
///     passo, e ele é grande: índice da Mojang, bibliotecas, assets, nativos,
///     classpath e um JSON de versão por loader.
///     Dizê-lo com todas as letras é melhor do que um botão que pisca e não faz
///     nada — e melhor do que esconder o botão, que mandaria o jogador procurar
///     o que não existe.
/// </summary>
public sealed class PendingGameLauncher : IGameLauncher
{
    public Task<GameLaunchResult> LaunchAsync(
        GameLaunchRequest request,
        IProgress<GameLaunchProgress>? progress,
        CancellationToken ct) =>
        Task.FromResult(GameLaunchResult.Failed(
            "Esta versão do launcher ainda não abre o jogo. Está tudo pronto do lado da conta e "
            + "do Java — falta a próxima atualização."));
}
