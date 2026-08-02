using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Escreve a pasta de instância de um servidor a partir de uma versão do
///     modpack: mods e overrides resolvidos do blob store. É o que o orquestrador
///     monta como volume no container itzg. Preserva world/ e dados do jogador —
///     trocar a versão reescreve mods, nunca apaga o mundo.
/// </summary>
public interface IInstanceMaterializer
{
    Task MaterializeAsync(Guid gameServerId, ModpackVersion version, CancellationToken ct);

    /// <summary>Caminho raiz da instância no host, para o orquestrador montar -v.</summary>
    string GetInstancePath(Guid gameServerId);

    /// <summary>
    ///     Apaga a pasta inteira da instância (mods, configs E o mundo).
    ///     Irreversível. Idempotente: silêncio se a pasta já não existe.
    /// </summary>
    Task DeleteInstanceAsync(Guid gameServerId, CancellationToken ct);
}
