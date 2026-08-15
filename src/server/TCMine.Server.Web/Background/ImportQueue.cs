using System.Threading.Channels;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Fila de importações de packs externos, sobre um Channel — mesmo desenho
///     da fila de ingestão.
///     O job carrega o id do ImportRequest: é ele que o worker apaga ao terminar,
///     e é a sobra dessa linha no arranque que denuncia uma queda no meio.
/// </summary>
public sealed class ImportQueue : IImportQueue
{
    private readonly Channel<ImportJob> _channel = Channel.CreateUnbounded<ImportJob>();

    public ValueTask EnqueueAsync(ImportRequest request, CancellationToken ct) =>
        _channel.Writer.WriteAsync(
            new ImportJob(request.Id, request.Origin, request.ProjectId, request.FileId, request.DisplayName),
            ct);

    public IAsyncEnumerable<ImportJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed record ImportJob(Guid Id, ModFileOrigin Origin, string ProjectId, string? FileId, string DisplayName);
