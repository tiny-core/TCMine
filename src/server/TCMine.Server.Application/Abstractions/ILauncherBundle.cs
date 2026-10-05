namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     O launcher que vem DENTRO da imagem do servidor.
///     Cada imagem leva o launcher compilado do mesmo commit, e o servidor o
///     publica no próprio feed de atualização ao arrancar — com o endereço desta
///     instalação embutido, para o jogador não digitar nada no primeiro uso.
///     É isso que impede lançar um servidor que exige launcher novo e esquecer
///     de publicar o launcher: os dois saem juntos.
/// </summary>
public interface ILauncherBundle
{
    /// <summary>
    ///     Publica o launcher embutido no feed, se ainda não estiver lá com esta
    ///     versão e este endereço. Idempotente: no arranque seguinte, sem mudança,
    ///     não faz nada.
    /// </summary>
    /// <param name="serverUrl">Endereço público desta instalação; nulo gera um launcher genérico.</param>
    Task<LauncherBundleOutcome> PublishAsync(Uri? serverUrl, CancellationToken ct);
}

public enum LauncherBundleOutcome
{
    /// <summary>Imagem sem launcher (build local) ou publicação desligada.</summary>
    NoBundle,

    /// <summary>A mesma versão, com o mesmo endereço, já está no feed.</summary>
    UpToDate,

    /// <summary>
    ///     O feed já tem uma versão MAIOR (publicada à mão). Não se rebaixa:
    ///     o Velopack não "atualiza" para trás, e o jogador ficaria com a mais nova.
    /// </summary>
    NewerAlreadyPublished,

    Published
}
