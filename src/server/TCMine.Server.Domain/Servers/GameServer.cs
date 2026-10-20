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

    /// <summary>
    ///     Rótulo do nome deste servidor no túnel da Cloudflare ("atm10-tunel"),
    ///     já normalizado. Nulo = sem reserva pelo túnel. Com o domínio da
    ///     instalação vira um CNAME para o túnel e uma entrada TCP nele, mantidos
    ///     pela sincronização do DNS. Único entre os servidores, e diferente de
    ///     todo subdomínio.
    /// </summary>
    public string? TunnelLabel { get; set; }

    /// <summary>
    ///     As opções de jogo definidas para este servidor (dificuldade, distância
    ///     de visão…), em JSON: só as que o admin definiu. Nulo = nenhuma, e vale
    ///     tudo o que o modpack traz. Ler e gravar sempre por
    ///     <see cref="GameOptions" />, que é quem sabe o que é válido.
    /// </summary>
    public string? OptionsJson { get; set; }

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
    ///     Quando o TCMine viu pela primeira vez que este servidor tem mundo.
    ///     Null = ainda não tem.
    ///     É o seam do backup: trocar a versão de um servidor COM mundo exige
    ///     snapshot antes (mods removidos/rebaixados podem corromper o save). Sem
    ///     mundo, a troca é o re-apontar simples e imediato.
    ///     Quem grava (só por <c>IServerRepository.MarkWorldInitializedAsync</c>,
    ///     uma vez): a primeira entrada em Running, que é quando o jogo já gerou o
    ///     level.dat, e o preenchimento do arranque, que olha a pasta. Até a 0.13.0
    ///     ninguém gravava, e a troca de versão rodava sem backup em servidor com
    ///     mundo. Num servidor de antes disso a data é a da descoberta, não a do
    ///     primeiro boot.
    /// </summary>
    public DateTimeOffset? WorldInitializedAt { get; set; }

    /// <summary>Já tem mundo gravado? Deriva de WorldInitializedAt.</summary>
    public bool HasWorld => WorldInitializedAt is not null;

    /// <summary>
    ///     Token de concorrência do formulário do servidor (fase 6.3). Sobe a cada
    ///     gravação do formulário; o repositório só grava se a linha ainda tiver o
    ///     número que o formulário leu. Assim, de duas pessoas salvando ao mesmo
    ///     tempo, a segunda recebe um aviso em vez de apagar o que a primeira
    ///     salvou. As gravações de uma coluna (status, container, versão fixada,
    ///     nuvem, mundo) NÃO o sobem: ligar o servidor com o formulário aberto
    ///     não faz o salvar falhar, porque nada do que o formulário edita mudou.
    ///     Chama-se RowVersion, e não Version, para não se confundir com a
    ///     versão do modpack fixada no servidor.
    /// </summary>
    public long RowVersion { get; private set; }

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
