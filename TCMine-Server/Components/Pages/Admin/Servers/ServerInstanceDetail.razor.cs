using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Server.Components.Pages.Admin.Servers.Dialogs;
using TCMine_Server.Services;
using TCMine_Server.Infrastructure.ServerInstances;

namespace TCMine_Server.Components.Pages.Admin.Servers;

/// <summary>
/// Detalhe de uma instância: ações de ciclo de vida, console ao vivo (stream de logs do container +
/// envio de comandos) e edição dos arquivos de config. O console transmite os logs no próprio circuito
/// Blazor usando o <c>ContainerId</c> (sem tocar no DbContext durante o stream); o
/// <see cref="DisposeAsync"/> cancela o stream ao sair da página.
/// </summary>
public partial class ServerInstanceDetail : ComponentBase, IAsyncDisposable
{
    [Inject] private ServerInstanceService Service { get; set; } = null!;
    [Inject] private ProvisioningCoordinator Provisioner { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Parameter] public Guid Id { get; set; }

    private ServerInstanceDetailDto? _detail;

    // ── Provisionamento (via coordenador de fundo; reconecta após refresh) ──────────────────────────
    private ProvisionJobView? _job;            // job em curso/concluído desta instância (null = nenhum)
    private ElementReference _jobStepsEl;       // container rolável do log de passos → auto-scroll
    private bool _scrollJobPending;             // sinaliza ao OnAfterRender para descer ao passo atual

    // ── Console ───────────────────────────────────────────────────────────────────────────────────
    private readonly StringBuilder _logBuffer = new();
    private string _log = string.Empty;
    private string _command = string.Empty;
    private CancellationTokenSource? _streamCts;
    private string? _streamingContainerId; // container cujo stream está ativo (evita duplicar)
    private bool _streamingFollow; // modo do stream ativo: follow (rodando) vs estático (parado/crash)
    private bool _consoleScrollPending; // sinaliza ao OnAfterRender para descer o console à última linha

    // ── Status de jogadores (Server List Ping, enquanto rodando) ──────────────────────────────────
    private ServerPing? _ping;
    private CancellationTokenSource? _pingCts;

    // Limite do buffer de log em memória (caracteres) — descarta o começo quando estoura
    private const int MaxLogChars = 100_000;

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando instância…", LoadAsync);

        // Reconecta a um provisionamento em andamento (ex.: o usuário deu refresh no meio) e passa a ouvir
        // o coordenador para ver o progresso ao vivo.
        if (Provisioner.IsRunning(Id)) _job = Provisioner.Get(Id);
        Provisioner.Changed += OnJobChanged;
    }

    // Reage à mudança de status (start/stop) ligando/desligando o stream de logs
    protected override void OnParametersSet()
    {
        SyncLogStream();
    }

    // Mantém o console preso na última linha (auto-scroll) sempre que chega log novo
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Auto-scroll do log de passos do provisionamento (mantém o passo atual visível)
        if (_scrollJobPending)
        {
            _scrollJobPending = false;
            try { await JS.InvokeVoidAsync("tcmineScrollToBottom", _jobStepsEl); }
            catch { /* painel não renderizado — ignora */ }
        }

        if (_consoleScrollPending)
        {
            _consoleScrollPending = false;
            try { await JS.InvokeVoidAsync("tcmineConsole.scrollToBottom"); }
            catch { /* console não renderizado (outra aba) — ignora */ }
        }
    }

    // Há um provisionamento em andamento para esta instância?
    private bool IsProvisioning => _job is { State: ProvisionState.Running };

    // Status "global" do painel: a fase atual (rótulo antes de " — "); o detalhe fica na lista de passos
    private string JobGlobalStatus
    {
        get
        {
            var last = _job is { Steps.Count: > 0 } ? _job.Steps[^1] : "Provisionando…";
            var i = last.IndexOf(" — ", StringComparison.Ordinal);
            return i < 0 ? last : last[..i];
        }
    }

    private async Task LoadAsync()
    {
        _detail = await Service.GetDetailAsync(Id);
    }

    // ── Ciclo de vida ───────────────────────────────────────────────────────────────────────────────

    // Dispara o provisionamento no COORDENADOR de fundo (não no circuito): não bloqueia a UI, sobrevive a
    // um refresh de página (a página reconecta ao progresso) e é retomado no boot se o server cair.
    private void StartProvision()
    {
        var applying = _detail is { Provisioned: true, IsStale: true };
        Provisioner.Start(Id, applying);
        _job = Provisioner.Get(Id);
        _scrollJobPending = true;
    }

    // Coordenador avisou que o job mudou (roda fora do circuito → InvokeAsync). Atualiza o painel; ao
    // concluir com sucesso, recarrega o detalhe e some com o painel; em erro, mantém os passos + o erro.
    private void OnJobChanged(Guid id)
    {
        if (id != Id) return;
        _ = InvokeAsync(async () =>
        {
            var view = Provisioner.Get(Id);
            if (view is null) return;

            if (view.State == ProvisionState.Running)
            {
                _job = view;
                _scrollJobPending = true;
                StateHasChanged();
                return;
            }

            if (view.State == ProvisionState.Succeeded)
            {
                _job = null;
                await LoadAsync();
                SyncLogStream();
                Snackbar.Add("Provisionamento concluído.", Severity.Success);
            }
            else
            {
                _job = view;
                Snackbar.Add(view.Error ?? "Falha no provisionamento.", Severity.Error);
            }

            StateHasChanged();
        });
    }

    // Fecha o painel de um provisionamento que falhou (o usuário leu o erro)
    private void DismissJob()
    {
        _job = null;
    }

    private async Task StartAsync()
    {
        await RunAndReload("Iniciando servidor…", () => Service.StartAsync(Id), "Servidor iniciando.");
    }

    private async Task StopAsync()
    {
        await RunAndReload("Parando servidor…", () => Service.StopAsync(Id), "Servidor parado.");
    }

    private async Task EditAsync()
    {
        if (_detail is null) return;

        var parameters = new DialogParameters<ServerInstanceEditDialog> { { x => x.Instance, _detail.Edit } };
        var dialog = await DialogService.ShowAsync<ServerInstanceEditDialog>(
            "Editar instância", parameters, ServerInstances.EditOptions());
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not ServerInstanceEditDto dto) return;

        await RunAndReload("Salvando instância…", () => Service.UpdateAsync(dto), "Instância atualizada.");
    }

    // Envelope comum: operação sob overlay + recarrega o detalhe + snackbar; erros viram snackbar.
    // A operação corre em Task.Run (fora do dispatcher do circuito): operações como o provisionamento
    // têm trabalho síncrono pesado (link de arquivos) que, no dispatcher, congelaria a UI e engoliria o
    // progresso ao vivo. Fora dele, o overlay renderiza cada passo. As portas usadas são sequenciais
    // (sem acesso concorrente ao DbContext), então rodar noutra thread é seguro.
    private async Task RunAndReload(string message, Func<Task> operation, string success)
    {
        try
        {
            await Busy.RunAsync(message, async () =>
            {
                await Task.Run(operation);
                await LoadAsync();
            });
            SyncLogStream();
            Snackbar.Add(success, Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    // ── Console: stream de logs ───────────────────────────────────────────────────────────────────────

    // Mostra os logs sempre que há container: ao vivo (follow) se rodando; estático (último log, ex.: o
    // crash) se parado/crashou. Reinicia o stream se o container ou o modo (follow) mudou.
    private void SyncLogStream()
    {
        var containerId = _detail?.ContainerId;
        var follow = _detail is { Status: ServerInstanceStatus.Running };

        if (containerId is not null)
        {
            if (_streamingContainerId != containerId || _streamingFollow != follow)
            {
                StopLogStream();
                StartLogStream(containerId, follow);
            }
        }
        else if (_streamingContainerId is not null)
        {
            StopLogStream();
        }

        // Ping de jogadores: liga enquanto Running, desliga caso contrário
        if (follow && _pingCts is null) StartPing();
        else if (!follow && _pingCts is not null) StopPing();
    }

    // ── Ping de jogadores ───────────────────────────────────────────────────────────────────────────

    private void StartPing()
    {
        _pingCts = new CancellationTokenSource();
        _ = PingLoopAsync(_pingCts.Token);
    }

    private void StopPing()
    {
        _pingCts?.Cancel();
        _pingCts?.Dispose();
        _pingCts = null;
        _ping = null;
    }

    // Faz o ping a cada 10s usando host/porta do detalhe (sem tocar no DbContext)
    private async Task PingLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                _ping = await Service.PingAsync(_detail?.Edit.PublicAddress, _detail?.Edit.Port ?? 0, ct);
                await InvokeAsync(StateHasChanged);
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Saída normal (parou / saiu da página)
        }
    }

    private void StartLogStream(string containerId, bool follow)
    {
        _logBuffer.Clear();
        _log = string.Empty;
        _streamingContainerId = containerId;
        _streamingFollow = follow;
        _streamCts = new CancellationTokenSource();
        _ = StreamLoopAsync(containerId, follow, _streamCts.Token);
    }

    private void StopLogStream()
    {
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;
        _streamingContainerId = null;
    }

    // Lê o stream de logs e atualiza a UI; encerra silenciosamente no cancelamento (saída da página/parada)
    private async Task StreamLoopAsync(string containerId, bool follow, CancellationToken ct)
    {
        try
        {
            await foreach (var chunk in Service.StreamLogsAsync(containerId, follow, ct: ct))
            {
                _logBuffer.Append(chunk);
                if (_logBuffer.Length > MaxLogChars)
                    _logBuffer.Remove(0, _logBuffer.Length - MaxLogChars);

                _log = _logBuffer.ToString();
                _consoleScrollPending = true; // desce para a última linha após o render
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Saída normal (cancelado ao sair da página ou parar o servidor)
        }
        catch (Exception ex)
        {
            _log += $"\n[stream interrompido: {ex.Message}]\n";
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(_command) || _detail?.ContainerId is not { } containerId) return;

        var cmd = _command;
        _command = string.Empty;
        try
        {
            await Service.SendCommandAsync(containerId, cmd);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao enviar comando: {ex.Message}", Severity.Error);
        }
    }

    private async Task OnCommandKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SendCommandAsync();
    }

    // ── Apresentação do status ────────────────────────────────────────────────────────────────────────

    private static string StatusLabel(ServerInstanceStatus s)
    {
        return s switch
        {
            ServerInstanceStatus.Running => "Em execução",
            ServerInstanceStatus.Starting => "Iniciando",
            ServerInstanceStatus.Stopping => "Parando",
            ServerInstanceStatus.Provisioning => "Provisionando",
            ServerInstanceStatus.Crashed => "Falhou",
            _ => "Parado"
        };
    }

    private static Color StatusColor(ServerInstanceStatus s)
    {
        return s switch
        {
            ServerInstanceStatus.Running => Color.Success,
            ServerInstanceStatus.Starting or ServerInstanceStatus.Stopping => Color.Info,
            ServerInstanceStatus.Provisioning => Color.Info,
            ServerInstanceStatus.Crashed => Color.Error,
            _ => Color.Default
        };
    }

    public ValueTask DisposeAsync()
    {
        Provisioner.Changed -= OnJobChanged;
        StopLogStream();
        StopPing();
        return ValueTask.CompletedTask;
    }
}
