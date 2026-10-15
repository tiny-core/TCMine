using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     O que fazer com um servidor, visto deste computador.
///     Função pura, e no Core por isso: é a decisão que o botão de cada linha da
///     aba Servidores toma, e ela tem casos que merecem teste — o pack trocado no
///     servidor, a versão que mudou com a pasta já instalada.
/// </summary>
public static class ServerEntry
{
    /// <param name="instance">A instância DESTE servidor, ou nula se ainda não há.</param>
    public static ServerEntryAction Of(GameServerDto server, InstanceManifest? instance) =>
        server.AccessState switch
        {
            ServerAccessState.None => ServerEntryAction.RequestAccess,
            ServerAccessState.Pending => ServerEntryAction.Pending,

            // Instância de outro pack é como nenhuma: o PrepareServerInstance
            // a devolve às pessoais e instala outra, então o botão diz isso.
            _ when instance is null || instance.ModpackId != server.ModpackId => ServerEntryAction.Install,
            _ when instance?.ModpackVersionId == server.ModpackVersionId => ServerEntryAction.Play,
            _ => ServerEntryAction.UpdateAndPlay
        };

    /// <summary>
    ///     Por que não dá para entrar AGORA, ou nulo. Não impede instalar: deixar
    ///     a pasta pronta com o servidor desligado é justamente o que poupa a
    ///     espera quando ele voltar.
    /// </summary>
    public static string? Unavailable(GameServerDto server) => server.Status switch
    {
        // Iniciando é diferente de desligado: aqui o jogador só precisa de
        // esperar, e o botão libera sozinho quando o jogo aceitar jogadores.
        GameServerStatus.Starting => "O servidor está iniciando. O botão libera quando ele aceitar jogadores.",
        GameServerStatus.Running => null,
        _ => "O servidor não está no ar agora."
    };
}

public enum ServerEntryAction
{
    /// <summary>Com whitelist, e o jogador ainda não pediu.</summary>
    RequestAccess,

    /// <summary>Pediu; o dono ainda não decidiu.</summary>
    Pending,

    /// <summary>Pode entrar, mas não há a pasta deste servidor aqui.</summary>
    Install,

    /// <summary>A pasta está na versão do servidor.</summary>
    Play,

    /// <summary>A pasta está noutra versão; entrar alinha antes de abrir.</summary>
    UpdateAndPlay
}
