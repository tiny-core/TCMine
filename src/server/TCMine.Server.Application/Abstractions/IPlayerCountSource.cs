namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Quantos jogadores estão online em cada servidor, na última amostragem.
///     Não é coluna do <see cref="Server.Domain.Servers.GameServer" /> de
///     propósito: o número muda a cada minuto e não sobrevive a nada. Persisti-lo
///     seria uma escrita no banco por servidor a cada coleta para guardar um
///     valor que já nasce velho — e, depois de um reinício, um número gravado
///     seria exibido com confiança estando errado.
/// </summary>
public interface IPlayerCountSource
{
    /// <summary>
    ///     Nulo quando não se sabe: servidor parado, ainda não amostrado, ou o
    ///     comando devolveu algo que não deu para ler. A interface mostra um
    ///     traço nesse caso — zero seria afirmar que está vazio.
    /// </summary>
    int? TryGet(Guid gameServerId);

    /// <summary>
    ///     Maior contagem já vista hoje (UTC) para este servidor, mesmo que ele
    ///     já tenha parado — o pico de ontem não aparece nem some de repente à
    ///     meia-noite, ele só deixa de valer. Nulo quando não houve amostra
    ///     nenhuma hoje.
    /// </summary>
    int? PeakToday(Guid gameServerId);
}
