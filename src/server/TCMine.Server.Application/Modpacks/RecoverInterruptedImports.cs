using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     No arranque, retoma as importações que o processo anterior não terminou.
///     A regra é simples porque o rastro é: quem termina uma importação apaga a
///     linha, dê certo ou dê errado. Então toda linha que sobrou aqui significa
///     uma coisa só — o processo morreu no meio dela.
/// </summary>
public sealed class RecoverInterruptedImports(
    IImportRequestRepository requests,
    IImportQueue queue)
{
    /// <summary>Quantas importações voltaram para a fila.</summary>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var pendentes = await requests.ListAllAsync(ct);

        var retomadas = 0;
        foreach (var request in pendentes)
        {
            if (!request.TryRegisterRecovery())
            {
                // Sem cota e sem retomada: a linha precisa sair, senão o pack
                // fica bloqueado para sempre pela checagem de duplicata e o
                // admin não consegue nem tentar de novo a mão.
                await requests.RemoveAsync(request.Id, ct);
                continue;
            }

            await requests.UpdateAsync(request, ct);
            await queue.EnqueueAsync(request, ct);
            retomadas++;
        }

        return retomadas;
    }
}
