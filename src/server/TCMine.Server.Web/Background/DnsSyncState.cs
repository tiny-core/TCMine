using System.Threading.Channels;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Dns;

namespace TCMine.Server.Web.Background;

/// <summary>
///     O que liga quem pede uma sincronização de DNS a quem a executa, e guarda o
///     resultado da última para a tela mostrar.
///     Singleton e em memória: um pedido perdido num reinício não faz falta,
///     porque o <see cref="DnsSyncWorker" /> sincroniza de qualquer forma ao
///     arrancar e de tempos em tempos.
/// </summary>
public sealed class DnsSyncState : IDnsSyncSignal
{
    // Capacidade 1 e descarte do excedente: dez servidores editados em seguida
    // são UM motivo para sincronizar, não dez sincronizações em fila.
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>A última sincronização, automática ou pedida na tela. Nulo = nenhuma ainda.</summary>
    public DnsSyncOutcome? Last { get; private set; }

    public void Request() => _requests.Writer.TryWrite(true);

    public void Record(DnsSyncReport report) => Last = new DnsSyncOutcome(report, DateTimeOffset.UtcNow);

    /// <summary>
    ///     Espera um pedido ou o fim do intervalo, o que vier primeiro.
    ///     Verdadeiro = alguém pediu.
    /// </summary>
    public async Task<bool> WaitAsync(TimeSpan interval, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(interval);

        try
        {
            await _requests.Reader.ReadAsync(timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Esquece os pedidos acumulados: a sincronização que vem a seguir atende todos.</summary>
    public void Drain()
    {
        while (_requests.Reader.TryRead(out _))
        {
        }
    }
}

public sealed record DnsSyncOutcome(DnsSyncReport Report, DateTimeOffset At);
