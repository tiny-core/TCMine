using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Marca os servidores que já têm mundo no disco e nunca receberam a marca.
///     Até a 0.13.0 ninguém gravava <c>WorldInitializedAt</c>, então todo servidor
///     parecia "sem mundo" e a troca de versão rodava sem backup. A entrada em
///     Running marca quem está no ar; isto cobre quem está parado, sem esperar
///     que alguém o ligue — a troca de versão acontece justamente com ele parado.
///     Roda no arranque e é idempotente: marcado uma vez, o servidor sai da conta.
/// </summary>
public sealed class BackfillWorldInitialized(IServerRepository servers, IWorldBackupStore worlds)
{
    /// <summary>Quantos servidores ganharam a marca.</summary>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var marked = 0;

        // Todos os servidores, sem filtro no banco: são poucos por natureza (um
        // homelab tem um punhado), e a pergunta que decide é ao disco.
        foreach (var server in await servers.ListAllAsync(ct))
        {
            ct.ThrowIfCancellationRequested();

            if (server.HasWorld || !worlds.HasWorld(server.Id))
                continue;

            if (await servers.MarkWorldInitializedAsync(server.Id, DateTimeOffset.UtcNow, ct))
                marked++;
        }

        return marked;
    }
}
