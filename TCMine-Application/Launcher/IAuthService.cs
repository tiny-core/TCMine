using TCMine_Domain.Launcher;

namespace TCMine_Application.Launcher;

/// <summary>Porta do login Microsoft do jogador (implementada via MSAL/CmlLib na infraestrutura).</summary>
public interface IAuthService
{
    /// <summary>Sessão atual, ou null se não autenticado.</summary>
    PlayerSession? Current { get; }

    /// <summary>Login silencioso (token em cache). Null = sem conta válida.</summary>
    Task<PlayerSession?> TryLoginSilentAsync(CancellationToken ct = default);

    /// <summary>Login completo (popup interativo se preciso).</summary>
    Task<PlayerSession> LoginAsync(CancellationToken ct = default);

    /// <summary>Logout (limpa a conta e o cache).</summary>
    Task SignOutAsync(CancellationToken ct = default);
}
