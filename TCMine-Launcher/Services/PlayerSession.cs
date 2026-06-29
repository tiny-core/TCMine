using CmlLib.Core.Auth;

namespace TCMine_Launcher.Services;

/// <summary>
/// Identidade do jogador para a UI (derivada da <see cref="MSession"/> do MSAL/CmlLib). O token e o
/// refresh ficam no cache do MSAL — aqui só guardamos o que a UI mostra.
/// </summary>
public sealed record PlayerSession(string Uuid, string Username)
{
    public static PlayerSession From(MSession session) => new(session.UUID ?? "", session.Username ?? "");
}
