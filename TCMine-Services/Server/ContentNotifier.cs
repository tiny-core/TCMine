using System.Collections.Concurrent;
using System.Threading.Channels;

namespace TCMine_Services.Server;

/// <summary>
/// Sinaliza aos launchers ligados (via SSE em <c>/events</c>) que o conteúdo público
/// mudou — modpacks criados/editados/eliminados ou (no futuro) novidades. Mantém uma
/// <see cref="Version"/> incremental e transmite-a a todos os subscritores quando
/// <see cref="Bump"/> é chamado.
///
/// Singleton (estado partilhado em memória). O launcher fixa a versão inicial como
/// baseline e recarrega o catálogo sempre que recebe uma versão maior.
/// </summary>
public sealed class ContentNotifier
{
    // Cada subscritor é um canal; o byte é só um valor-presença (usamos como conjunto concorrente).
    private readonly ConcurrentDictionary<Channel<long>, byte> _subscribers = new();
    private long _version = 1;

    /// <summary>Versão atual do conteúdo (incrementa a cada alteração no admin).</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>Incrementa a versão e notifica todos os launchers ligados.</summary>
    public void Bump()
    {
        var v = Interlocked.Increment(ref _version);
        foreach (var sub in _subscribers.Keys)
            sub.Writer.TryWrite(v);
    }

    /// <summary>Regista um subscritor (um launcher ligado ao stream SSE).</summary>
    public ChannelReader<long> Subscribe(out Channel<long> channel)
    {
        // Capacidade 1 + DropOldest: só interessa a versão mais recente; se o cliente está lento,
        // descartamos a intermédia em vez de acumular um backlog de notificações.
        channel = Channel.CreateBounded<long>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        _subscribers.TryAdd(channel, 0);
        return channel.Reader;
    }

    /// <summary>Remove o subscritor (cliente desligou) e fecha o canal.</summary>
    public void Unsubscribe(Channel<long> channel)
    {
        _subscribers.TryRemove(channel, out _);
        channel.Writer.TryComplete();
    }
}