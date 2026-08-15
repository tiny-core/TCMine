using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Enfileira a importação de um pack externo para rodar em background.
///     Mesmo motivo da fila de ingestão: baixar o zip de um pack grande e gravar
///     milhares de overrides leva minutos, e prender o diálogo até o fim passa a
///     impressão de que o sistema travou.
///     Recebe o pedido já gravado: quem enfileira é o ImportScheduler, e o id do
///     ImportRequest é também o id do acompanhamento — assim a tela mostra o
///     progresso antes de existir um modpack para apontar.
/// </summary>
public interface IImportQueue
{
    ValueTask EnqueueAsync(ImportRequest request, CancellationToken ct);
}
