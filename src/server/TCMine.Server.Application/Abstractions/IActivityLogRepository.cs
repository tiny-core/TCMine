using TCMine.Server.Domain.Common;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     O feed "Atividade recente" da Visão geral. Quem grava (login pelo
///     painel, publicação de versão, servidor caído, backup) injeta isto
///     direto — não é regra de negócio, é só "guarde que isto aconteceu".
///     <see cref="AddAsync" /> nunca deve derrubar quem chamou: a implementação
///     engole e registra a própria falha. Perder uma linha do feed é aceitável;
///     fazer uma publicação ou um backup falharem porque o log auxiliar não
///     escreveu não é.
/// </summary>
public interface IActivityLogRepository
{
    Task AddAsync(ActivityEvent entry, CancellationToken ct);

    Task<IReadOnlyList<ActivityEvent>> ListRecentAsync(int count, CancellationToken ct);
}
