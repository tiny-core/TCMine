namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Guarda segredos do jogador — na prática, o refresh token da Microsoft.
///     A implementação de Windows usa DPAPI, que amarra o dado à conta de
///     usuário do sistema. Guardar token em JSON puro significa que qualquer
///     programa rodando na máquina consegue ler e se passar pelo jogador.
/// </summary>
public interface ICredentialStore
{
    Task<string?> ReadAsync(string key, CancellationToken ct);
    Task WriteAsync(string key, string value, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}