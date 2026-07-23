using TCMine.Contracts;

namespace TCMine.Launcher.Core.Connectivity;

/// <summary>
///     Descobre a qual servidor este launcher pertence.
///     Ordem de resolução:
///     1. tcmine.json na raiz da instalação
///     2. Token embutido no nome do instalador (só no primeiro run)
///     3. Deep link tcmine://pair?url=...
///     4. tela pedindo a URL manualmente
///     O passo 4 não é opcional. Se o antivírus colocar o json em quarentena ou
///     o arquivo corromper, sem essa tela o launcher vira um tijolo e o jogador
///     não tem como se recuperar sozinho.
/// </summary>
public interface ILauncherConfigProvider
{
    Task<LauncherConfig?> TryLoadAsync(CancellationToken ct);

    Task SaveAsync(LauncherConfig config, CancellationToken ct);
}