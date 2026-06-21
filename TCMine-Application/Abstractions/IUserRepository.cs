using TCMine_Domain.Entities;

namespace TCMine_Application.Abstractions;

/// <summary>
/// Acesso a dados dos usuários do painel. A camada Application depende desta porta;
/// a implementação concreta (EF Core) vive na Infrastructure. Regras de negócio (normalização
/// do login, hash de senha) ficam no serviço de aplicação — o repositório só persiste/consulta.
/// </summary>
public interface IUserRepository
{
    /// <summary>Existe ao menos um usuário? (detecção de primeira execução)</summary>
    Task<bool> AnyAsync(CancellationToken ct = default);

    /// <summary>Usuário por login (já normalizado), ou null.</summary>
    Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>Usuário por id, rastreado para edição, ou null.</summary>
    Task<UserEntity?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Todos os usuários, ordenados por login (tela de gestão).</summary>
    Task<List<UserEntity>> GetAllOrderedAsync(CancellationToken ct = default);

    /// <summary>True se já existe um usuário com esse login (já normalizado).</summary>
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>Quantos Owners ativos existem (impede remover/rebaixar o último).</summary>
    Task<int> CountActiveOwnersAsync(CancellationToken ct = default);

    /// <summary>Marca um novo usuário para inserção (efetivado no <see cref="SaveChangesAsync"/>).</summary>
    void Add(UserEntity user);

    /// <summary>Marca um usuário para remoção (efetivado no <see cref="SaveChangesAsync"/>).</summary>
    void Remove(UserEntity user);

    /// <summary>Persiste as alterações pendentes (insert/update/delete).</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
