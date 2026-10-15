using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Um túnel até o servidor de jogo pela Cloudflare (entrada TCP de um
///     Cloudflare Tunnel), para quem não alcança o caminho direto: CGNAT, porta
///     fechada, rede que bloqueia. O jogo não sabe falar com o túnel; ele entra
///     numa porta local, e o túnel carrega os bytes até o servidor.
///     Nada se instala no PC do jogador: é uma conexão segura (wss) aberta pelo
///     próprio launcher — a razão de não ser uma rede virtual como o Hamachi,
///     que pediria driver e permissão de administrador.
/// </summary>
public interface IGameTunnel
{
    /// <summary>
    ///     Confere que o túnel atende (uma conexão de teste) e abre a porta local.
    ///     Nulo quando o túnel não respondeu — quem chama decide o que tentar no
    ///     lugar. Nunca lança por causa da rede.
    /// </summary>
    Task<IGameTunnelSession?> OpenAsync(string hostname, CancellationToken ct);
}

/// <summary>A porta local aberta. Fechar a sessão fecha a porta e as conexões que passam por ela.</summary>
public interface IGameTunnelSession : IAsyncDisposable
{
    /// <summary>Onde o jogo deve entrar (127.0.0.1 e uma porta livre).</summary>
    ServerAddress LocalAddress { get; }
}
