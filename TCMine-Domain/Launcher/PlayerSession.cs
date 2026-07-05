namespace TCMine_Domain.Launcher;

/// <summary>
///     Identidade do jogador para a UI (derivada do login). Valor puro de domínio — a obtenção do token
///     (MSAL/CmlLib) é infraestrutura; aqui só o que a UI mostra.
/// </summary>
public sealed record PlayerSession(string Uuid, string Username)
{
    /// <summary>Rótulo do tipo de conta (sempre Microsoft no TCMine).</summary>
    public string AccountLabel => "Conta Microsoft";

    /// <summary>URL da cabeça da skin (mc-heads). Null sem UUID.</summary>
    public string? HeadUrl => string.IsNullOrEmpty(Uuid) ? null : $"https://mc-heads.net/avatar/{Uuid}/128";

    /// <summary>Até 2 iniciais em maiúsculas para o avatar.</summary>
    public string Initials => Username.Length > 0 ? Username[..Math.Min(2, Username.Length)].ToUpperInvariant() : "??";
}