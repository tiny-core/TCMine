using System.ComponentModel.DataAnnotations;
using TCMine_Data.Authentication;

namespace TCMine_Data.Entities;

/// <summary>
/// Um usuário do painel admin. Substitui a antiga senha única (ADMIN_PASSWORD).
/// A senha nunca é guardada em texto — só o <see cref="PasswordHash"/> (PBKDF2 via
/// <c>PasswordHasher</c>). O primeiro usuário é criado no setup de primeira execução
/// com papel <see cref="UserRole.Owner"/>.
/// </summary>
public class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Nome de login, único (case-insensitive, na prática — normalizamos para minúsculas)
    [MaxLength(60)] public string Username { get; set; } = string.Empty;

    // Hash da senha (formato do PasswordHasher); nunca a senha em texto
    [MaxLength(400)] public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Viewer;

    // Desativado não consegue conectar, mas o histórico é preservado
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Atualizado a cada login bem-sucedido (null = nunca logou)
    public DateTime? LastLoginAt { get; set; }
}