using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Web.Components.Features.Servers;

public partial class ServerConsole : ComponentBase, IAsyncDisposable
{
    /// <summary>
    ///     Quantas linhas ficam na tela. Um servidor com mods cospe milhares por
    ///     hora, e cada linha é um nó no DOM do circuito — sem teto, a aba do
    ///     admin engasga sozinha depois de um tempo aberta.
    /// </summary>
    private const int MaxLines = 500;

    /// <summary>
    ///     De quanto em quanto tempo a tela é redesenhada.
    ///     Renderizar por linha inundaria o circuito no arranque do servidor,
    ///     quando centenas chegam em rajada. Meio segundo é imperceptível para
    ///     quem lê e reduz o tráfego a uma fração.
    /// </summary>
    private static readonly TimeSpan RenderInterval = TimeSpan.FromMilliseconds(500);

    private readonly string _consoleId = $"console-{Guid.CreateVersion7():N}";

    private readonly Lock _gate = new();
    private readonly Queue<string> _lines = new();

    private bool _autoScroll = true;
    private CancellationTokenSource? _cts;
    private string? _error;
    private IJSObjectReference? _module;
    private bool _pending;
    private Timer? _renderTimer;
    private bool _streaming;

    [Parameter] [EditorRequired] public GameServer Server { get; set; } = default!;

    [Inject] private IServerOrchestrator Orchestrator { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        if (_module is { } module)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuito já caiu: não há o que liberar do outro lado.
            }
        }

        GC.SuppressFinalize(this);
    }

    protected override async Task OnParametersSetAsync()
    {
        // Ligar/desligar o servidor troca o estado do componente sem recriá-lo:
        // o stream precisa começar e terminar junto.
        var deveriaSeguir = Server.Status is GameServerStatus.Running;

        if (deveriaSeguir && !_streaming)
            Start();
        else if (!deveriaSeguir && _streaming)
            await StopAsync();
    }

    private void Start()
    {
        _streaming = true;
        _error = null;
        _cts = new CancellationTokenSource();

        // Redesenha em intervalo fixo, não a cada linha.
        _renderTimer = new Timer(_ =>
        {
            lock (_gate)
            {
                if (!_pending)
                    return;

                _pending = false;
            }

            _ = InvokeAsync(async () =>
            {
                StateHasChanged();

                if (_autoScroll)
                    await ScrollToBottomAsync();
            });
        }, null, RenderInterval, RenderInterval);

        _ = PumpAsync(_cts.Token);
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var line in Orchestrator.StreamLogsAsync(Server.Id, ct))
            {
                lock (_gate)
                {
                    _lines.Enqueue(line);
                    while (_lines.Count > MaxLines)
                        _lines.Dequeue();

                    _pending = true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Saída normal: o admin fechou o painel ou o servidor parou.
        }
        catch (Exception ex)
        {
            _error = $"Não foi possível seguir o console: {ex.Message}";
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            _streaming = false;
        }
    }

    private async Task StopAsync()
    {
        if (_cts is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _cts = null;
        }

        if (_renderTimer is { } timer)
        {
            await timer.DisposeAsync();
            _renderTimer = null;
        }

        _streaming = false;
    }

    private void Clear()
    {
        lock (_gate)
        {
            _lines.Clear();
        }
    }

    private async Task ScrollToBottomAsync()
    {
        try
        {
            _module ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./Components/Features/Servers/ServerConsole.razor.js");

            await _module.InvokeVoidAsync("scrollToBottom", _consoleId);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            // Circuito caindo, elemento já removido ou pré-renderização: rolar é
            // cosmético e nunca pode derrubar o componente.
        }
    }
}
