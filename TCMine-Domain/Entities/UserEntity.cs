using System.ComponentModel.DataAnnotations;
using TCMine_Domain.Identity;

namespace TCMine_Domain.Entities;

/// <summary>
/// Um usuário do painel admin. Substitui a antiga senha única (ADMIN_PASSWORD).
/// A senha nunca é guardada em texto — só o <see cref="PasswordHash"/> (PBKDF2 via
/// <c>PasswordHasher</c>). O primeiro usuário é criado no setup de primeira execução
/// com papel <see cref="UserRole.Owner"/>.
/// </summary>
public class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Nome de login, único (case-insensitive, na prática — normalizamos para minúsculas)
    /// </summary>
    [MaxLength(60)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Hash da senha (formato do PasswordHasher); nunca a senha em texto
    /// </summary>
    [MaxLength(400)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define a função atribuída ao usuário, definindo suas permissões e nível de acesso dentro do sistema.
    /// </summary>
    /// <remarks>
    /// Uma enumeração <see cref="UserRole"/> fornece as funções predefinidas:
    /// <code>
    /// - Owner:    Controle total sobre o sistema.
    /// - Admin:    Privilégios administrativos de alto nível.
    /// - Operator: Acesso operacional limitado.
    /// - Viewer:   Acesso somente leitura.
    /// </code>
    /// <para>
    /// O papel é armazenado como uma string no banco de dados para melhor legibilidade e é aplicado
    /// em toda a aplicação para gerenciar o acesso e o comportamento do usuário.
    /// </para>
    /// </remarks>
    public UserRole Role { get; set; } = UserRole.Viewer;

    /// <summary>
    /// Desativado não consegue conectar, mas o histórico é preservado
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Atualizado a cada login bem-sucedido (null = nunca logou)
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}