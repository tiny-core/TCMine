using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TCMine.Contracts.Hubs;
using MudBlazor;
using Microsoft.AspNetCore.Components.Web;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Hubs;

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

    /// <summary>
    ///     Chave sintética para o broadcaster — não é uma conexão de Hub de
    ///     verdade, só o que ele usa para contar ouvintes e ligar/desligar o
    ///     bombeamento por servidor.
    /// </summary>
    private readonly string _subscriptionId = $"admin-console-{Guid.CreateVersion7():N}";

    private readonly Lock _gate = new();
    private readonly Queue<ConsoleEntry> _lines = new();

    /// <summary>Os comandos desta sessão, do mais antigo ao mais novo, para ↑/↓.</summary>
    private readonly List<string> _history = [];

    private const int MaxHistory = 50;

    private int _historyIndex;
    private string _input = "";
    private string _filter = "";
    private bool _sending;
    private bool _showReference;
    private ServerRoleDto? _role;
    private MudTextField<string>? _inputField;

    private bool _autoScroll = true;
    private IJSObjectReference? _module;
    private bool _pending;
    private Timer? _renderTimer;
    private bool _streaming;

    [Parameter] [EditorRequired] public GameServer Server { get; set; } = default!;

    [Inject] private ConsoleBroadcaster Broadcaster { get; set; } = default!;
    [Inject] private ICurrentUserScope Scope { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private SendServerCommand SendCommand { get; set; } = default!;

    /// <summary>Moderador para cima: a mesma régua do caso de uso, que confere de novo a cada envio.</summary>
    private bool CanSend => _role is { } role && role >= ServerRoleDto.Moderator;

    /// <summary>A sintaxe do comando que está a ser digitado, se ele estiver no catálogo.</summary>
    private string? InputHelper =>
        MinecraftCommands.Find(_input.Trim().TrimStart('/').Split(' ', 2)[0]) is { } cmd
            ? cmd.Syntax
            : "Enter envia · ↑ e ↓ percorrem os comandos já enviados";

    /// <summary>
    ///     O catálogo filtrado e, para moderador, só o que ele pode mandar —
    ///     mostrar o resto seria oferecer o que o servidor vai recusar.
    /// </summary>
    private IEnumerable<IGrouping<MinecraftCommandCategory, MinecraftCommand>> ReferenceGroups =>
        MinecraftCommands.All
            .Where(c => _role is { } role && ConsoleCommandPolicy.IsAllowed(role, c.Name))
            .Where(c => string.IsNullOrWhiteSpace(_filter)
                        || c.Name.Contains(_filter.Trim(), StringComparison.OrdinalIgnoreCase)
                        || c.Description.Contains(_filter.Trim(), StringComparison.OrdinalIgnoreCase))
            .GroupBy(c => c.Category);

    protected override async Task OnInitializedAsync() =>
        _role = await Scope.GetRoleAsync(Server.Id, CancellationToken.None);

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

        Broadcaster.LineReceived += OnLineReceived;

        // Id sintético: o broadcaster só usa como chave para contar ouvintes e
        // decidir se já existe (ou precisa abrir) um bombeamento para este
        // servidor — é o mesmo mecanismo que já evita dez streams do Docker
        // para dez abas do launcher, agora reaproveitado aqui em vez desta
        // tela abrir o próprio stream por cima.
        if (Scope.UserId is { } userId)
            Broadcaster.Subscribe(_subscriptionId, userId, Server.Id);
    }

    private void OnLineReceived(Guid serverId, ConsoleLineDto line)
    {
        if (serverId != Server.Id)
            return;

        lock (_gate)
        {
            // O painel exibe só o texto; o canal é usado pelo bombeamento para
            // o launcher, que precisa distinguir o log da partida do estouro
            // da JVM.
            Append(line.Text, ConsoleEntryKind.Log);
            _pending = true;
        }
    }

    /// <summary>Chamar com o <c>_gate</c> tomado.</summary>
    private void Append(string text, ConsoleEntryKind kind)
    {
        _lines.Enqueue(new ConsoleEntry(text, kind));
        while (_lines.Count > MaxLines)
            _lines.Dequeue();
    }

    /// <summary>
    ///     Manda a linha pelo mesmo caso de uso do launcher: papel, allowlist e
    ///     forma são conferidos lá, não aqui. O eco e a resposta entram no
    ///     próprio console, junto do log, porque é ali que o admin está a olhar.
    /// </summary>
    private async Task SendAsync()
    {
        var line = _input.Trim();
        if (line.Length is 0 || _sending)
            return;

        _sending = true;

        if (_history.Count is 0 || _history[^1] != line)
        {
            _history.Add(line);
            if (_history.Count > MaxHistory)
                _history.RemoveAt(0);
        }

        _historyIndex = _history.Count;

        lock (_gate)
            Append($"> {line}", ConsoleEntryKind.Command);

        try
        {
            var result = await SendCommand.HandleLineAsync(Server.Id, line, CancellationToken.None);

            lock (_gate)
            {
                if (!result.Succeeded)
                    Append(result.Error!, ConsoleEntryKind.Error);
                else if (string.IsNullOrWhiteSpace(result.Value))
                    Append("(sem resposta)", ConsoleEntryKind.Reply);
                else
                {
                    foreach (var reply in result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        Append(reply.TrimEnd('\r'), ConsoleEntryKind.Reply);
                }
            }

            if (result.Succeeded)
                _input = "";
        }
        finally
        {
            _sending = false;
        }

        StateHasChanged();
        await ScrollToBottomAsync();

        if (_inputField is not null)
            await _inputField.FocusAsync();
    }

    private void OnInputChanged(string value) => _input = value ?? "";

    private async Task OnInputKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Enter":
                await SendAsync();
                break;
            case "ArrowUp" when _history.Count > 0:
                _historyIndex = Math.Max(0, _historyIndex - 1);
                _input = _history[_historyIndex];
                break;
            case "ArrowDown" when _history.Count > 0:
                _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
                _input = _historyIndex < _history.Count ? _history[_historyIndex] : "";
                break;
        }
    }

    /// <summary>Coloca o comando na caixa, pronto para os argumentos, e devolve o foco a ela.</summary>
    private async Task UseCommandAsync(MinecraftCommand command)
    {
        _input = command.Name + " ";

        if (_inputField is not null)
            await _inputField.FocusAsync();
    }

    private static string EntryClass(ConsoleEntryKind kind) => kind switch
    {
        ConsoleEntryKind.Command => "tc-console-command",
        ConsoleEntryKind.Reply => "tc-console-reply",
        ConsoleEntryKind.Error => "tc-console-error",
        _ => ""
    };

    private static string CategoryLabel(MinecraftCommandCategory category) => category switch
    {
        MinecraftCommandCategory.Players => "Jogadores",
        MinecraftCommandCategory.Moderation => "Moderação",
        MinecraftCommandCategory.World => "Mundo",
        MinecraftCommandCategory.ItemsAndEffects => "Itens e efeitos",
        _ => "Servidor"
    };

    private async Task StopAsync()
    {
        Broadcaster.LineReceived -= OnLineReceived;
        Broadcaster.Unsubscribe(_subscriptionId, Server.Id);

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

/// <summary>De onde veio a linha do console: do log do jogo ou da conversa do admin com ele.</summary>
public enum ConsoleEntryKind
{
    Log,
    Command,
    Reply,
    Error
}

public readonly record struct ConsoleEntry(string Text, ConsoleEntryKind Kind);
