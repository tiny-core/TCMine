using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Quais instâncias têm versão mais nova à espera.
///     Existia um buraco aqui: a tela marcava um pack como "Instalado" comparando
///     só o id do modpack, e ignorando a versão. O administrador publicava a 1.1
///     e o jogador não tinha como saber nem como atualizar — a promessa central
///     do produto, o servidor publicar e o launcher reconciliar, ficava sem
///     gatilho na interface.
/// </summary>
public sealed class CheckInstanceUpdates(IServerConnection connection)
{
    /// <summary>
    ///     A versão mais recente de cada instância que está desatualizada.
    ///     Instâncias em dia não aparecem, e o dicionário vazio é a resposta
    ///     normal — inclusive sem rede, porque não saber não é o mesmo que haver
    ///     novidade, e uma tela cheia de "atualizar" que falha ao clicar seria
    ///     pior do que não mostrar nada.
    /// </summary>
    public async Task<IReadOnlyDictionary<InstanceKey, ModpackVersionDto>> HandleAsync(
        IReadOnlyList<InstalledInstance> instances,
        CancellationToken ct)
    {
        var novidades = new Dictionary<InstanceKey, ModpackVersionDto>();

        // Uma consulta por MODPACK, não por instância: duas instalações do mesmo
        // pack — que agora são possíveis — pediriam a mesma resposta duas vezes.
        foreach (var grupo in instances.GroupBy(i => i.Manifest.ModpackId))
        {
            var ultima = await UltimaAsync(grupo.Key, ct);

            if (ultima is null)
                continue;

            foreach (var instancia in grupo.Where(i => i.Manifest.ModpackVersionId != ultima.Id))
                novidades[instancia.Key] = ultima;
        }

        return novidades;
    }

    private async Task<ModpackVersionDto?> UltimaAsync(Guid modpackId, CancellationToken ct)
    {
        try
        {
            return await connection.GetLatestVersionAsync(modpackId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Canal em baixo, ou pack removido do catálogo. Silêncio é a resposta
            // certa: esta consulta é um extra sobre uma tela que tem de servir
            // offline, e falhar nela não pode impedir ver o que está instalado.
            return null;
        }
    }
}
