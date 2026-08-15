using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Background;
using TCMine.UI.Shared.Formatting;

namespace TCMine.Server.Web.Components.Features.Servers;

public partial class WorldBackupsPanel : ComponentBase, IDisposable
{
    private List<WorldBackup> _backups = [];
    private bool _isBusy;
    private Guid _jobId;

    [Parameter] [EditorRequired] public GameServer Server { get; set; } = default!;

    /// <summary>Avisa a tela de cima quando o mundo muda — o status pode ter mudado junto.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    [Inject] private IServerRepository Repository { get; set; } = default!;
    [Inject] private CreateWorldBackup CreateUseCase { get; set; } = default!;
    [Inject] private RestoreWorldBackup RestoreUseCase { get; set; } = default!;
    [Inject] private DeleteWorldBackup DeleteUseCase { get; set; } = default!;
    [Inject] private JobProgressRegistry Jobs { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private JobProgress? Progress => _jobId == Guid.Empty ? null : Jobs.Get(_jobId);

    /// <summary>
    ///     Backup vale parado OU no ar — no ar o autosave é pausado por RCON.
    ///     Estados de transição (iniciando, parando) não valem: o jogo pode não
    ///     responder ao comando e a cópia sairia às cegas.
    /// </summary>
    private bool CanBackup =>
        Server.Status is GameServerStatus.Stopped or GameServerStatus.Crashed or GameServerStatus.Running;

    /// <summary>
    ///     Restaurar continua exigindo parado: aqui os arquivos são
    ///     SUBSTITUÍDOS, e não há comando de RCON que impeça o jogo de reabrir o
    ///     que estamos trocando debaixo dele.
    /// </summary>
    private bool CanRestore => Server.Status is GameServerStatus.Stopped or GameServerStatus.Crashed;

    private string BackupHint => Server.Status switch
    {
        GameServerStatus.Running => "Pausa o autosave, salva o mundo e religa — sem derrubar ninguém",
        GameServerStatus.Stopped or GameServerStatus.Crashed => "Salva o mundo num .zip",
        _ => "Espere o servidor assentar"
    };

    public void Dispose()
    {
        Jobs.Changed -= OnJobChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        Jobs.Changed += OnJobChanged;
        await LoadAsync();
    }

    private void OnJobChanged() => _ = InvokeAsync(StateHasChanged);

    private async Task LoadAsync() =>
        _backups = [.. await Repository.ListBackupsAsync(Server.Id, CancellationToken.None)];

    private Task CreateAsync() => RunAsync(async () =>
    {
        var result = await CreateUseCase.HandleAsync(
            Server.Id, null, CancellationToken.None, WorldBackupReason.Manual, _jobId);

        if (result.Succeeded)
            Snackbar.Add("Mundo salvo.", Severity.Success);
        else
            Snackbar.Add(result.Error!, Severity.Error);
    });

    private async Task RestoreAsync(WorldBackup backup)
    {
        // Restaurar substitui o mundo atual: é destrutivo, e o admin precisa
        // saber que perde o que jogou desde o snapshot.
        var deOutraVersao = backup.ModpackVersionId != Server.ModpackVersionId;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Restaurar mundo",
            $"O mundo atual é substituído pelo backup de "
            + $"{backup.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm} ({HumanSize.Bytes(backup.SizeBytes)}). "
            + "Tudo o que foi jogado depois dele se perde."
            + (deOutraVersao
                ? $" Atenção: este backup é da versão {backup.ModpackVersionLabel ?? "anterior"}, "
                  + "diferente da que o servidor usa agora — o mundo pode não abrir direito."
                : ""),
            "Restaurar", cancelText: "Cancelar");

        if (confirm is not true)
            return;

        await RunAsync(async () =>
        {
            var result = await RestoreUseCase.HandleAsync(
                backup.Id, CancellationToken.None, deOutraVersao, _jobId);

            if (result.Succeeded)
                Snackbar.Add("Mundo restaurado.", Severity.Success);
            else
                Snackbar.Add(result.Error!, Severity.Error);
        });
    }

    private async Task DeleteAsync(WorldBackup backup)
    {
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar backup",
            $"Apagar o backup de {backup.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}? "
            + "O arquivo sai do disco de vez.",
            "Apagar", cancelText: "Cancelar");

        if (confirm is not true)
            return;

        await RunAsync(async () =>
        {
            var result = await DeleteUseCase.HandleAsync(backup.Id, CancellationToken.None);

            if (!result.Succeeded)
                Snackbar.Add(result.Error!, Severity.Error);
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        _isBusy = true;
        _jobId = Guid.CreateVersion7();

        try
        {
            await action();
            await LoadAsync();
            await OnChanged.InvokeAsync();
        }
        finally
        {
            _isBusy = false;
            _jobId = Guid.Empty;
        }
    }
}
