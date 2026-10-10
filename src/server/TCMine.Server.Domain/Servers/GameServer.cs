using TCMine.Contracts.Servers;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Servers;

/// <summary>Limites da porta de jogo, partilhados pelo domínio e pelas regras.</summary>
public static class GamePortDefaults
{
    /// <summary>Porta padrão do Minecraft; também o início da faixa padrão.</summary>
    public const int First = 25565;

    /// <summary>Fim da faixa padrão: 35 portas, um redirecionamento só no roteador.</summary>
    public const int Last = 25599;
}

public sealed class GameServer : Entity, IOwnedEntity
{
    public required string Name { get; set; }
    public required Guid ModpackId { get; set; }

    /// <summary>
    ///     A versão é pinada aqui, no servidor, e não no modpack.
    ///     Sem isso você não consegue atualizar um servidor de cada vez, nem
    ///     manter um de testes na versão nova enquanto o principal fica estável.
    /// </summary>
    public required Guid ModpackVersionId { get; set; }

    /// <summary>
    ///     O que o admin escreveu como endereço — NÃO o que os jogadores recebem.
    ///     Vazio = automático (host público da instalação). Sem ":porta", a porta
    ///     é a <see cref="GamePort" />. Com ":porta", vale o que está escrito.
    ///     Quem mostra ou envia o endereço passa por <see cref="GameAddress.Resolve" />.
    /// </summary>
    public required string ConnectAddress { get; set; }

    /// <summary>
    ///     Porta do HOST em que o container do jogo publica a 25565 dele.
    ///     Única entre todos os servidores, parados incluídos: a porta de um
    ///     servidor nunca muda sozinha, e dois nunca disputam a mesma.
    ///     Antes ela era adivinhada a partir do texto do
    ///     <see cref="ConnectAddress" /> — um campo fazia dois papéis, e dois
    ///     servidores sem ":porta" no endereço caíam ambos na 25565.
    /// </summary>
    public int GamePort { get; set; } = GamePortDefaults.First;

    /// <summary>
    ///     Rótulo de DNS deste servidor ("sobrevivencia"), já normalizado. Nulo =
    ///     sem nome próprio. Com o domínio da instalação vira
    ///     <c>sobrevivencia.exemplo.com</c>, mantido na Cloudflare por um registro
    ///     SRV (ver <see cref="GameDns" />).
    ///     Único entre os servidores: dois com o mesmo nome disputariam o mesmo
    ///     registro.
    /// </summary>
    public string? Subdomain { get; set; }

    public GameServerStatus Status { get; set; } = GameServerStatus.Stopped;

    /// <summary>ID do container itzg/minecraft-server. Nulo se nunca foi criado.</summary>
    public string? ContainerId { get; set; }

    public int MemoryMb { get; set; } = 4096;
    public int MaxPlayers { get; set; } = 20;

    /// <summary>
    ///     Só quem tem convite entra.
    ///     Ligada por padrão porque é o comportamento que quase todo mundo quer
    ///     de um servidor privado, e porque errar para o lado aberto é o erro
    ///     caro: um servidor exposto na internet sem lista é achado por scanner
    ///     em horas.
    ///     O Minecraft não tem senha de entrada — a lista de servidores do
    ///     cliente guarda endereço e nome, mais nada. A whitelist é o mecanismo
    ///     que o jogo realmente oferece, e ela prende à CONTA, não ao launcher.
    /// </summary>
    public bool WhitelistEnabled { get; set; } = true;

    /// <summary>
    ///     Senha do RCON. NUNCA sai do servidor: não vai em DTO, não vai em log,
    ///     não aparece na UI. O launcher pede um comando pelo Hub e o servidor é
    ///     quem traduz para RCON — quem tem a senha tem controle total da máquina
    ///     do jogo.
    /// </summary>
    public required string RconSecret { get; set; }

    /// <summary>
    ///     Quando o mundo deste servidor foi inicializado (primeiro boot que gerou
    ///     o level.dat). Null = nunca ligou, ainda nao tem mundo.
    ///     E o seam do backup: trocar a versao de um servidor COM mundo exige
    ///     snapshot antes (mods removidos/rebaixados podem corromper o save). Sem
    ///     mundo, a troca e o re-apontar simples e imediato. Nada preenche isto na
    ///     fatia 1 — so a orquestracao (fatia 3) o fara ao subir o container.
    /// </summary>
    public DateTimeOffset? WorldInitializedAt { get; set; }

    /// <summary>Já tem mundo gravado? Deriva de WorldInitializedAt.</summary>
    public bool HasWorld => WorldInitializedAt is not null;

    /// <summary>
    ///     Nuvem de itens (mod tccloud) que este servidor usa. Nulo = nuvem
    ///     desligada aqui (ex.: servidor de testes).
    /// </summary>
    public Guid? CloudVaultId { get; private set; }

    public Guid OwnerId { get; set; }

    /// <summary>
    ///     Liga a uma nuvem. A regra que importa é a do dono: um servidor só
    ///     enxerga a nuvem do próprio dono. Sem ela, quem tem um servidor poderia
    ///     ler e gravar os itens dos jogadores de outra pessoa.
    /// </summary>
    public void AttachToCloudVault(CloudVault vault)
    {
        if (vault.OwnerId != OwnerId)
            throw new InvalidOperationException("O servidor só pode usar uma nuvem do mesmo dono.");
        CloudVaultId = vault.Id;
        Touch();
    }

    public void DetachFromCloudVault()
    {
        CloudVaultId = null;
        Touch();
    }
}
