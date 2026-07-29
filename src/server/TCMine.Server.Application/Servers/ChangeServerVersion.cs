using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Troca a versão fixada de um servidor. Para servidor SEM mundo é o re-apontar
///     simples (os bytes da nova versão já estão no blob store). Servidor COM mundo
///     é barrado aqui: trocar mods de um mundo já gerado pode corromper o save
///     (mod removido = registry faltando; downgrade = formato de dados incompatível).
///     A troca segura — parar container, snapshot do mundo, recriar — é fatia 3/3.5.
/// </summary>
public sealed class ChangeServerVersion(
    IServerRepository servers,
    IModpackRepository modpacks)
{
    public async Task<Result> HandleAsync(Guid serverId, Guid targetVersionId, CancellationToken ct)
    {
        var server = await servers.GetByIdAsync(serverId, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");

        if (server.ModpackVersionId == targetVersionId)
            return Result.Fail("O servidor já está nesta versão.");

        // Barreira do mundo: enquanto não há backup+orquestração, não deixamos
        // trocar a versão de um servidor com mundo gerado.
        if (server.HasWorld)
            return Result.Fail(
                "Este servidor já tem mundo. Trocar a versão pode corromper o save e exige backup — "
                + "disponível quando a orquestração estiver ativa.");

        // A versão alvo tem de ser do mesmo modpack e instalável (publicada ou
        // arquivada). Draft/Resolving/Failed não servem para rodar.
        var target = await modpacks.GetVersionAsync(targetVersionId, ct);
        if (target is null || target.ModpackId != server.ModpackId)
            return Result.Fail("Versão inválida para este modpack.");

        if (target.State is not (ModpackVersionState.Ready or ModpackVersionState.Archived) || target.IsPreRelease)
            return Result.Fail("Só é possível fixar uma versão estável publicada ou arquivada.");

        server.ModpackVersionId = targetVersionId;
        server.UpdatedAt = DateTimeOffset.UtcNow;

        await servers.UpdateAsync(server, ct);
        return Result.Success();
    }
}