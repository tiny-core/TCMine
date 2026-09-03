using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     O que o jogador pode instalar, e onde pode jogar.
///     São duas listas do servidor porque são dois assuntos — o catálogo é do
///     administrador, os servidores dependem de quem está pedindo —, mas na tela
///     valem juntas: um modpack sem servidor se instala e se joga sozinho, e um
///     com servidor mostra onde entrar.
/// </summary>
public sealed class LoadCatalog(IServerConnection connection)
{
    public async Task<CatalogView> HandleAsync(Uri serverUrl, CancellationToken ct)
    {
        try
        {
            // Abre o canal aqui, e não no login, para que o botão de "tentar de
            // novo" da tela resolva também o caso de a conexão ter caído. Ligar
            // no login deixaria a tela sem saída: o catálogo falharia para
            // sempre até o jogador sair e entrar outra vez.
            if (!connection.IsConnected)
                await connection.ConnectAsync(serverUrl, ct);

            // Em paralelo: são consultas independentes no mesmo canal, e a tela
            // só existe quando as duas chegam.
            var modpacks = connection.GetModpacksAsync(ct);
            var servers = connection.GetServersAsync(ct);

            await Task.WhenAll(modpacks, servers);

            return CatalogView.Of(Join(await modpacks, await servers));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // O canal caiu, ou o servidor recusou. Falhar com mensagem deixa a
            // tela oferecer "tentar de novo"; uma exceção subindo daqui deixaria
            // um spinner girando para sempre.
            return CatalogView.Failure("Não foi possível carregar o catálogo. " + ex.Message);
        }
    }

    /// <summary>
    ///     Casa cada modpack com os servidores que o usam.
    ///     Função pura, e separada por isso: é a única lógica de verdade deste
    ///     caso de uso, e assim ela é testável sem canal nenhum.
    ///     Servidores apontando para um modpack fora da lista são ignorados —
    ///     acontece quando o jogador tem acesso a um servidor de um pack que foi
    ///     removido do catálogo, e inventar uma entrada para ele mostraria um
    ///     card sem nome nem versão.
    /// </summary>
    public static IReadOnlyList<CatalogEntry> Join(
        IReadOnlyList<ModpackDto> modpacks,
        IReadOnlyList<GameServerDto> servers)
    {
        var porModpack = servers
            .GroupBy(s => s.ModpackId)
            .ToDictionary(g => g.Key, IReadOnlyList<GameServerDto> (g) => [.. g]);

        return
        [
            .. modpacks
                // Ordem alfabética, e não "com servidor primeiro": a lista precisa
                // ser previsível entre aberturas, e a posição de um card mudaria
                // sozinha toda vez que um servidor subisse ou caísse.
                .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(m => new CatalogEntry(
                    m,
                    porModpack.TryGetValue(m.Id, out var seus) ? seus : []))
        ];
    }
}

public sealed record CatalogEntry(ModpackDto Modpack, IReadOnlyList<GameServerDto> Servers)
{
    public bool HasServer => Servers.Count > 0;

    /// <summary>Algum servidor deste pack está no ar agora.</summary>
    public bool IsAnyServerRunning => Servers.Any(s => s.Status is GameServerStatus.Running);

    public int OnlinePlayers => Servers.Where(s => s.Status is GameServerStatus.Running).Sum(s => s.OnlinePlayers);
}

public sealed record CatalogView
{
    public required IReadOnlyList<CatalogEntry> Entries { get; init; }

    public string? Error { get; init; }

    public bool Failed => Error is not null;

    public bool IsEmpty => !Failed && Entries.Count is 0;

    public static CatalogView Of(IReadOnlyList<CatalogEntry> entries) => new() { Entries = entries };

    public static CatalogView Failure(string error) => new() { Entries = [], Error = error };
}
