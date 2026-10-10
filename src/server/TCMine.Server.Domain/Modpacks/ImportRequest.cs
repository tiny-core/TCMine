using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Modpacks;

/// <summary>
///     Rastro de uma importação em andamento.
///     Existe por uma razão só: a fila de importação vive em memória, e o trecho
///     mais demorado do trabalho — baixar e ler o zip do pack, que leva minutos —
///     acontece ANTES de qualquer gravação. Uma queda ali não deixava vestígio
///     nenhum: a barra de progresso sumia e nada tinha acontecido, sem sequer um
///     registro de que alguém pediu.
///     A linha é um rastro de trabalho em curso, não um histórico: quem termina a
///     importação a apaga, dê certo ou dê errado. Sobrar uma no arranque significa
///     exatamente uma coisa — o processo morreu no meio.
/// </summary>
public sealed class ImportRequest : Entity
{
    public const int MaxRecoveryAttempts = 3;
    public required ModFileOrigin Origin { get; set; }

    /// <summary>Id do pack na origem.</summary>
    public required string ProjectId { get; set; }

    /// <summary>Release fixada, quando o admin escolheu uma.</summary>
    public string? FileId { get; set; }

    /// <summary>Nome legível, para o log e o acompanhamento dizerem algo.</summary>
    public required string DisplayName { get; set; }

    /// <summary>
    ///     Quem pediu a importação. O trabalho roda no ImportWorker, fora de
    ///     qualquer requisição — lá não existe "usuário atual", e sem este campo
    ///     o modpack nascia sem dono e sem ninguém com papel de Owner.
    ///     Gravado (e não só passado pela fila) para sobreviver à retomada no
    ///     arranque. Nulo só em pedidos anteriores a esta coluna.
    /// </summary>
    public Guid? RequestedBy { get; set; }

    /// <summary>
    ///     Quantas vezes o arranque já retomou esta importação.
    ///     Mesmo freio da ingestão: se o que derruba o processo é este pack,
    ///     retomá-lo a cada arranque põe o servidor em ciclo de queda.
    /// </summary>
    public int RecoveryAttempts { get; set; }

    /// <summary>Devolve false quando a cota acabou — aí quem chama desiste.</summary>
    public bool TryRegisterRecovery()
    {
        if (RecoveryAttempts >= MaxRecoveryAttempts)
            return false;

        RecoveryAttempts++;
        return true;
    }
}
