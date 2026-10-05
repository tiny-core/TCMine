using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Public;

/// <summary>
///     O catálogo que qualquer visitante vê, sem sessão nenhuma — nome do
///     modpack, quem o mantém, e quais servidores estão de pé com quantos
///     jogadores. Mora num namespace à parte de <c>Modpacks</c>/<c>Servers</c>
///     de propósito: aqueles dois têm regra de arquitetura exigindo
///     <see cref="ICurrentUserScope" /> em todo caso de uso, e este é o único
///     ponto do sistema em que a ausência de sessão não é um caso a tratar —
///     é o requisito. Nada aqui vaza o que a auditoria de segurança já isolou:
///     sem RconSecret, sem lista de nomes de jogador (só a contagem).
/// </summary>
public sealed class GetPublicCatalog(
    IModpackRepository modpacks,
    IServerRepository servers,
    IModpackMembershipRepository memberships,
    IPlayerCountSource players)
{
    public async Task<PublicCatalogView> HandleAsync(CancellationToken ct)
    {
        var allModpacks = await modpacks.ListAsync(ct);
        var allServers = await servers.ListAllAsync(ct);

        var modpackViews = new List<PublicModpackView>(allModpacks.Count);
        foreach (var modpack in allModpacks)
        {
            var owner = await memberships.GetOwnerAsync(modpack.Id, ct);

            // Número de mods e tamanho são da versão PUBLICADA mais recente —
            // nunca de um rascunho, que pode estar vazio ou pela metade e não é
            // o que um visitante vai instalar. Sem versão publicada, os dois
            // ficam nulos: zero afirmaria um pack vazio que só não foi medido.
            var versions = await modpacks.ListVersionSummariesAsync(modpack.Id, ct);
            var published = versions
                .Where(v => v.State is ModpackVersionState.Ready)
                .MaxBy(v => v.Id); // GUID v7 = mais recente primeiro

            ModpackVersionStats? stats = null;
            if (published is not null)
            {
                var statsByVersion = await modpacks.GetVersionStatsAsync(modpack.Id, ct);
                stats = statsByVersion.GetValueOrDefault(published.Id);
            }

            modpackViews.Add(new PublicModpackView(
                modpack.Id,
                modpack.Slug,
                modpack.Name,
                modpack.Summary,
                // Mesma URL que ModpackMappings.ToDto monta no Web — não dá para
                // chamar aquele extension method daqui (Application não referencia
                // Web), então o caminho do endpoint de blobs se repete como literal.
                modpack.IconBlobSha256 is { } sha ? new Uri($"/api/v1/blobs/{sha}", UriKind.Relative) : null,
                modpack.MinecraftVersion,
                modpack.Loader,
                owner?.DisplayName,
                stats?.ModCount,
                stats?.TotalSizeBytes));
        }

        var modpackNames = allModpacks.ToDictionary(m => m.Id, m => m.Name);

        var serverViews = allServers
            .Select(server => new PublicServerView(
                server.Id,
                server.Name,
                modpackNames.GetValueOrDefault(server.ModpackId, "?"),
                server.Status,

                // Só pergunta a contagem de quem está de pé: um servidor parado
                // não tem container para o RCON perguntar, e o cache já some
                // com o último valor nesse caso (ver PlayerCountCache.Forget) —
                // isto é só reforço para quem lê este código sem abrir o cache.
                server.Status is GameServerStatus.Running ? players.TryGet(server.Id) : null,
                server.MaxPlayers))
            .ToList();

        return new PublicCatalogView(modpackViews, serverViews);
    }
}

public sealed record PublicModpackView(
    Guid Id,
    string Slug,
    string Name,
    string? Summary,
    Uri? IconUrl,
    string MinecraftVersion,
    ModLoader Loader,
    string? OwnerDisplayName,
    int? ModCount,
    long? TotalSizeBytes);

/// <summary>
///     Um servidor como QUALQUER visitante o vê. Sem o endereço, de propósito:
///     ele só sai para quem tem acesso aprovado (ver <c>GameServerDto.ConnectAddress</c>),
///     e a página pública o mostrava a todos — o que tirava o sentido de pedir
///     acesso e expunha o IP da máquina do jogo. Fora do modelo, e não só fora
///     do HTML: o que não sai daqui não vaza por nenhuma tela futura.
/// </summary>
public sealed record PublicServerView(
    Guid Id,
    string Name,
    string ModpackName,
    GameServerStatus Status,
    int? OnlinePlayers,
    int MaxPlayers);

public sealed record PublicCatalogView(
    IReadOnlyList<PublicModpackView> Modpacks,
    IReadOnlyList<PublicServerView> Servers);
