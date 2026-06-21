using TCMine_Domain.Entities;

namespace TCMine_Application.Abstractions;

/// <summary>
/// Acesso a dados das configs do jogador, por <c>(uuid, modpackId)</c>. São settings de jogo
/// guardadas como um zip, repostas quando o jogador entra noutro PC. <b>Last-write-wins</b>.
/// </summary>
public interface IPlayerConfigRepository
{
    /// <summary>Config do jogador para o modpack, ou null se ainda não sincronizou.</summary>
    Task<PlayerConfigEntity?> GetAsync(string uuid, string modpackId, CancellationToken ct = default);

    /// <summary>Insere ou substitui o zip de configs e devolve o instante da gravação.</summary>
    Task<DateTime> UpsertAsync(string uuid, string modpackId, byte[] zip, CancellationToken ct = default);
}
