using System.Collections.Concurrent;
using TCMine.Contracts.Hubs;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Hubs;

/// <summary>
///     Bombeia o console dos containers para os launchers assinados.
///     Um stream por SERVIDOR, não por conexão: dez jogadores acompanhando o
///     mesmo servidor abririam dez leituras do log no daemon do Docker, cada uma
///     recebendo os mesmos bytes. O grupo do SignalR já faz o fan-out; aqui só
///     precisa existir uma fonte.
///     Só bombeia enquanto houver alguém ouvindo. Manter o stream aberto para
///     todo servidor no ar gastaria uma conexão HTTP permanente por servidor
///     para jogar linhas fora — e o log de uma partida movimentada não é pouco
///     tráfego.
/// </summary>
public sealed partial class ConsoleBroadcaster(
    IServiceScopeFactory scopes,
    ILogger<ConsoleBroadcaster> logger) : IAsyncDisposable
{
    /// <summary>
    ///     Espera antes de reabrir um stream que caiu com assinantes ainda
    ///     ouvindo. Acontece de verdade sempre que o servidor reinicia: o
    ///     container some e volta, e sem religar o console silenciaria para
    ///     sempre sem ninguém entender por quê.
    /// </summary>
    private static readonly TimeSpan EsperaParaReligar = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<Guid, Bombeamento> _porServidor = new();

    /// <summary>
    ///     O que cada conexão assinou. Sem isto, uma queda de conexão deixaria o
    ///     contador de assinantes alto para sempre e o stream nunca fecharia —
    ///     e queda de conexão é o caso comum, não a exceção.
    /// </summary>
    private readonly ConcurrentDictionary<string, HashSet<Guid>> _porConexao = new();

    /// <summary>
    ///     De quem é cada conexão.
    ///     Mora aqui, e não numa classe à parte, porque este já é o único lugar
    ///     que sabe quem está assinado em quê — e é essa a pergunta que precisa
    ///     ser respondida para EXPULSAR alguém do console quando o papel dele
    ///     cai. Sem isso, o rebaixamento só valeria na próxima reconexão do
    ///     jogador, que é quando ele quiser.
    /// </summary>
    private readonly ConcurrentDictionary<string, Guid> _donoDaConexao = new();

    public async ValueTask DisposeAsync()
    {
        foreach (var bombeamento in _porServidor.Values)
            await bombeamento.PararAsync();

        _porServidor.Clear();
        _porConexao.Clear();
        _donoDaConexao.Clear();
    }

    public void Subscribe(string connectionId, Guid userId, Guid serverId)
    {
        _donoDaConexao[connectionId] = userId;

        var assinaturas = _porConexao.GetOrAdd(connectionId, _ => []);

        lock (assinaturas)
        {
            // Assinar duas vezes o mesmo servidor não conta dobrado: o contador
            // nunca voltaria a zero e o stream ficaria aberto para ninguém.
            if (!assinaturas.Add(serverId))
                return;
        }

        _porServidor.AddOrUpdate(
            serverId,
            _ => Iniciar(serverId),
            (_, existente) =>
            {
                existente.Entrou();
                return existente;
            });
    }

    public void Unsubscribe(string connectionId, Guid serverId)
    {
        if (!_porConexao.TryGetValue(connectionId, out var assinaturas))
            return;

        lock (assinaturas)
        {
            if (!assinaturas.Remove(serverId))
                return;
        }

        Soltar(serverId);
    }

    /// <summary>
    ///     Conexões de um usuário que acompanham um servidor.
    ///     É a lista que precisa sair do grupo quando o acesso dele muda.
    /// </summary>
    public IReadOnlyList<string> ConnectionsOf(Guid userId, Guid serverId)
    {
        List<string> encontradas = [];

        foreach (var (connectionId, dono) in _donoDaConexao)
        {
            if (dono != userId || !_porConexao.TryGetValue(connectionId, out var assinaturas))
                continue;

            lock (assinaturas)
            {
                if (assinaturas.Contains(serverId))
                    encontradas.Add(connectionId);
            }
        }

        return encontradas;
    }

    /// <summary>Conexão caiu: solta tudo o que ela segurava.</summary>
    public void Disconnect(string connectionId)
    {
        _donoDaConexao.TryRemove(connectionId, out _);

        if (!_porConexao.TryRemove(connectionId, out var assinaturas))
            return;

        Guid[] servidores;
        lock (assinaturas)
        {
            servidores = [.. assinaturas];
        }

        foreach (var serverId in servidores)
            Soltar(serverId);
    }

    private void Soltar(Guid serverId)
    {
        if (!_porServidor.TryGetValue(serverId, out var bombeamento))
            return;

        if (bombeamento.Saiu() is not 0)
            return;

        // Último ouvinte foi embora. Remove antes de cancelar para que uma
        // assinatura nova que chegue agora crie um bombeamento novo em vez de
        // se pendurar neste, que está morrendo.
        if (_porServidor.TryRemove(serverId, out var removido))
            _ = removido.PararAsync();
    }

    private Bombeamento Iniciar(Guid serverId)
    {
        var bombeamento = new Bombeamento();
        bombeamento.Tarefa = BombearAsync(serverId, bombeamento.Cancelamento.Token);
        return bombeamento;
    }

    private async Task BombearAsync(Guid serverId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Escopo por tentativa: o orquestrador é scoped, e um escopo que
                // vivesse tanto quanto o stream seguraria um DbContext aberto
                // por horas.
                await using var escopo = scopes.CreateAsyncScope();

                var orchestrator = escopo.ServiceProvider.GetRequiredService<IServerOrchestrator>();
                var notifier = escopo.ServiceProvider.GetRequiredService<IServerHubNotifier>();

                await foreach (var linha in orchestrator.StreamLogsAsync(serverId, ct))
                {
                    await notifier.NotifyConsoleLineAsync(
                        serverId,
                        new ConsoleLineDto(
                            DateTimeOffset.UtcNow,
                            linha.Text,
                            linha.IsError ? ConsoleStream.StdErr : ConsoleStream.StdOut),
                        ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // O console é acessório: o servidor de jogo segue rodando sem
                // ele. Derrubar o processo do painel por causa do log seria
                // trocar um problema pequeno por um grande.
                FalhaNoConsole(ex, serverId);
            }

            // Chegar aqui significa que o stream terminou — container parou,
            // reiniciou ou o daemon fechou a conexão. Ainda há quem ouça, então
            // tenta de novo.
            try
            {
                await Task.Delay(EsperaParaReligar, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Console do servidor {ServerId} caiu; religando.")]
    private partial void FalhaNoConsole(Exception ex, Guid serverId);

    /// <summary>
    ///     Um stream vivo e quantos o escutam.
    ///     O contador é o que decide quando parar, e por isso mora junto do
    ///     cancelamento: separá-los abriria a janela em que alguém entra
    ///     enquanto o outro decide sair.
    /// </summary>
    private sealed class Bombeamento
    {
        private int _assinantes = 1;

        public CancellationTokenSource Cancelamento { get; } = new();

        public Task? Tarefa { get; set; }

        public void Entrou() => Interlocked.Increment(ref _assinantes);

        public int Saiu() => Interlocked.Decrement(ref _assinantes);

        public async Task PararAsync()
        {
            await Cancelamento.CancelAsync();

            if (Tarefa is { } tarefa)
            {
                // Espera a tarefa sair antes de descartar o CTS: descartá-lo com
                // o bombeamento ainda lendo daria ObjectDisposedException no
                // meio do stream.
                try
                {
                    await tarefa;
                }
                catch (OperationCanceledException)
                {
                    // Saída esperada.
                }
            }

            Cancelamento.Dispose();
        }
    }
}
