using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using TCMine.Contracts.Hubs;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Hubs;

/// <summary>
///     Leva os avisos de mudança dos repositórios até os launchers, em rajadas.
///     Os repositórios só enfileiram (<see cref="Touch" /> não espera nada); este
///     serviço junta o que chegou numa janela curta e manda uma vez. Sem isto,
///     publicar uma versão ou salvar uma tela que grava várias linhas faria cada
///     launcher aberto rebuscar o catálogo várias vezes seguidas.
///     Vai para todos os launchers ligados: o aviso não leva dado nenhum, e a
///     rebusca de cada um passa pelas permissões da própria conta.
/// </summary>
public sealed partial class LauncherInvalidations(
    IHubContext<MainHub, ILauncherClient> hub,
    ILogger<LauncherInvalidations> logger) : BackgroundService, IResourceChanges
{
    /// <summary>
    ///     Quanto esperar pelo resto da rajada. Curto o bastante para a mudança
    ///     chegar ao jogador "na hora" (a meta da fase é menos de 2 s), longo o
    ///     bastante para juntar as escritas de um mesmo clique no painel.
    /// </summary>
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(250);

    private readonly Channel<ResourceChange> _changes =
        Channel.CreateUnbounded<ResourceChange>(new UnboundedChannelOptions { SingleReader = true });

    private readonly ILogger<LauncherInvalidations> _logger = logger;

    public void Touch(ResourceKind kind, Guid? id = null) => _changes.Writer.TryWrite(new ResourceChange(kind, id));

    /// <summary>
    ///     A mesma rajada, para quem está NESTE processo: as telas do painel.
    ///     Disparado depois de juntar, e não a cada <see cref="Touch" />, pelo
    ///     mesmo motivo do envio — uma tela a recarregar por cada linha gravada
    ///     seria pior do que não recarregar. Pode vir de qualquer thread.
    /// </summary>
    public event Action<ResourceKind, Guid?>? Sent;

    /// <summary>
    ///     Reduz uma rajada ao mínimo de avisos: um por tipo.
    ///     O id só segue quando a rajada inteira daquele tipo é sobre UM recurso;
    ///     com vários (ou com algum sem id), vai nulo, que quer dizer "vários".
    ///     O launcher de hoje rebusca o catálogo inteiro de qualquer forma, e um
    ///     aviso por recurso numa importação de cem itens seria cem mensagens a
    ///     cada launcher para o mesmo efeito.
    /// </summary>
    public static IReadOnlyList<ResourceChange> Collapse(IEnumerable<ResourceChange> changes) =>
    [
        .. changes
            .GroupBy(c => c.Kind)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var ids = g.Select(c => c.Id).Distinct().ToArray();

                return new ResourceChange(g.Key, ids is [{ } single] ? single : null);
            })
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _changes.Reader;

        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                await Task.Delay(Window, stoppingToken);

                var rajada = new List<ResourceChange>();

                while (reader.TryRead(out var change))
                    rajada.Add(change);

                foreach (var (kind, id) in Collapse(rajada))
                {
                    Sent?.Invoke(kind, id);

                    try
                    {
                        await hub.Clients.All.Invalidated(kind, id);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Um aviso perdido não é grave: o launcher rebusca tudo
                        // quando religa. Derrubar o serviço por isso calaria
                        // todos os avisos seguintes.
                        LogSendFailed(ex, kind);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // O painel está a parar.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível avisar os launchers de uma mudança em {Kind}.")]
    private partial void LogSendFailed(Exception ex, ResourceKind kind);
}

public readonly record struct ResourceChange(ResourceKind Kind, Guid? Id);
