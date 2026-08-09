using System.Threading.Channels;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Fila de importações de packs externos, sobre um Channel — mesmo desenho
///     da fila de ingestão.
/// </summary>
public sealed class ImportQueue : IImportQueue
{
    private readonly Channel<ImportJob> _channel = Channel.CreateUnbounded<ImportJob>();

    public async ValueTask<Guid> EnqueueAsync(
        ModFileOrigin origin, string projectId, string? fileId, string displayName, CancellationToken ct)
    {
        var job = new ImportJob(Guid.CreateVersion7(), origin, projectId, fileId, displayName);
        await _channel.Writer.WriteAsync(job, ct);
        return job.Id;
    }

    public IAsyncEnumerable<ImportJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed record ImportJob(Guid Id, ModFileOrigin Origin, string ProjectId, string? FileId, string DisplayName);
