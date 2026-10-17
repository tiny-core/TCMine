namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Reencontra um jogo que outro launcher abriu.
///     A saída dele (stdout) morreu com o launcher antigo — era um pipe para um
///     processo que já não existe —, então o processo devolvido lê o log que o
///     próprio Minecraft escreve no disco.
/// </summary>
public interface IGameProcessFinder
{
    /// <summary>
    ///     O processo, se ainda for o mesmo (PID E hora de início), ou nulo se já
    ///     saiu ou se o PID agora é de outro programa.
    /// </summary>
    IGameProcess? TryReattach(GameProcessIdentity identity, string instanceDirectory);
}
