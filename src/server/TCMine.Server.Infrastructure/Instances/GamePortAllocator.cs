using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Infrastructure.Docker;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Porta livre = fora do banco E fora do Docker.
///     O banco sabe dos servidores do TCMine, inclusive os parados (que guardam
///     a porta deles). O Docker sabe do resto que roda nesta máquina. O que
///     nenhum dos dois enxerga é um programa fora do Docker a usar a porta —
///     nesse caso o erro aparece ao iniciar o servidor, como sempre apareceu.
/// </summary>
public sealed partial class GamePortAllocator(
    IServerRepository servers,
    ISettingsRepository settings,
    DockerApiClient docker,
    ILogger<GamePortAllocator> logger) : IGamePortAllocator
{
    private readonly ILogger<GamePortAllocator> _logger = logger;

    public async Task<Result<int>> AllocateAsync(CancellationToken ct)
    {
        var config = await settings.GetAsync(ct);
        var (start, end) = GamePortRange.Normalize(config.GamePortRangeStart, config.GamePortRangeEnd);

        var used = new HashSet<int>((await servers.ListAllAsync(ct)).Select(s => s.GamePort));
        used.UnionWith(await DockerPortsAsync(null, ct));

        return GamePortRange.FirstFree(start, end, used) is { } port
            ? Result<int>.Success(port)
            : Result<int>.Fail(
                $"Não há porta livre na faixa {start}–{end}. Aumente a faixa nas configurações ou apague um servidor.");
    }

    public async Task<Result> EnsureAvailableAsync(int port, Guid? exceptServerId, CancellationToken ct)
    {
        if (!GamePortRange.IsValid(port))
            return Result.Fail($"A porta deve ficar entre {GamePortRange.Min} e {GamePortRange.Max}.");

        var owner = (await servers.ListAllAsync(ct))
            .FirstOrDefault(s => s.GamePort == port && s.Id != exceptServerId);
        if (owner is not null)
            return Result.Fail($"A porta {port} já é usada pelo servidor \"{owner.Name}\".");

        if ((await DockerPortsAsync(exceptServerId, ct)).Contains(port))
            return Result.Fail($"A porta {port} já está publicada por outro container nesta máquina.");

        return Result.Success();
    }

    /// <summary>
    ///     Portas TCP publicadas no host por containers em execução.
    ///     O container do próprio servidor em edição fica de fora: ele publica a
    ///     porta ATUAL desse servidor, que não pode contar contra ele.
    ///     Docker fora do ar não impede criar nem editar — a conferência cai para
    ///     só o banco, e o start é quem acusará um conflito real.
    /// </summary>
    private async Task<HashSet<int>> DockerPortsAsync(Guid? exceptServerId, CancellationToken ct)
    {
        try
        {
            var own = exceptServerId is { } id ? $"tcmine-{id}" : null;
            var containers = await docker.ListContainersAsync(false, ct);

            return
            [
                .. containers
                    .Where(c => own is null || c.Names.All(n => n.TrimStart('/') != own))
                    .SelectMany(c => c.Ports ?? [])
                    .Where(p => p is { PublicPort: > 0, Type: "tcp" })
                    .Select(p => p.PublicPort)
            ];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDockerUnavailable(ex);
            return [];
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Docker indisponível ao conferir portas; a conferência usa só o banco.")]
    private partial void LogDockerUnavailable(Exception ex);
}
