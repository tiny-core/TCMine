using TCMine.Contracts;

namespace TCMine.Launcher.Core.Connectivity;

/// <summary>
///     Descobre a qual servidor este launcher pertence.
///     Ordem de resolução:
///     1. tcmine.json na raiz da instalação
///     2. server.json que o servidor embutiu no instalador (só no primeiro run,
///        ver <see cref="IBundledServerAddress" />)
///     3. tela pedindo a URL manualmente (já preenchida com o passo 2, se houver)
///     O passo 3 não é opcional. Se o antivírus colocar o json em quarentena ou
///     o arquivo corromper, sem essa tela o launcher vira um tijolo e o jogador
///     não tem como se recuperar sozinho.
/// </summary>
public interface ILauncherConfigProvider
{
    Task<LauncherConfig?> TryLoadAsync(CancellationToken ct);

    Task SaveAsync(LauncherConfig config, CancellationToken ct);
}
