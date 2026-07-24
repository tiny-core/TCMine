using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Acesso à persistência de modpacks.
///     A interface vive na Application e a implementação na Infrastructure: é o
///     que permite testar os casos de uso com um repositório em memória, sem
///     subir banco, e mantém o EF Core fora desta camada.
///     Métodos deliberadamente estreitos — um por necessidade real de um caso
///     de uso. Repositório genérico com IQueryable exposto vaza detalhe de EF
///     para cima e torna a superfície difícil de testar.
/// </summary>
public interface IModpackRepository
{
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);

    Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct);

    void Add(Modpack modpack);

    /// <summary>
    ///     Persiste as mudanças rastreadas.
    ///     Separado do Add de propósito: um caso de uso pode alterar várias
    ///     entidades e gravar tudo numa transação só. É o padrão Unit of Work,
    ///     e o DbContext já é exatamente isso por baixo.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct);
}