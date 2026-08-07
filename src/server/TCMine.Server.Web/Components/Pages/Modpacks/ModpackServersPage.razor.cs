using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackServersPage
{
    private readonly HashSet<Guid> _busy = [];

    private bool _isLoading = true;
    private Modpack? _modpack;
    private List<GameServer> _servers = [];

    private Dictionary<Guid, ModpackVersion> _versionsById = new();
    [Parameter] public Guid ModpackId { get; set; }

    [Inject] private IServerRepository ServerRepository { get; set; } = default!;
    [Inject] private DeleteGameServer DeleteUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Inject] private IModpackRepository ModpackRepository { get; set; } = default!;

    [Inject] private StartGameServer StartUseCase { get; set; } = default!;
    [Inject] private StopGameServer StopUseCase { get; set; } = default!;
    [Inject] private IServerOrchestrator Orchestrator { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

    // Copia o endereço para a área de transferência — atalho útil para colar no
    // launcher/cliente. Usa a Clipboard API do navegador via interop.
    private async Task CopyAddress(string address)
    {
        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", address);
        Snackbar.Add("Endereço copiado.", Severity.Info);
    }

    private async Task Start(GameServer server)
    {
        _busy.Add(server.Id);
        try
        {
            var result = await StartUseCase.HandleAsync(server.Id, CancellationToken.None);
            if (!result.Succeeded)
                Snackbar.Add(result.Error!, Severity.Error);
            await LoadAsync(); // recarrega para o chip refletir o novo status
        }
        finally
        {
            _busy.Remove(server.Id);
        }
    }

    private async Task Stop(GameServer server)
    {
        _busy.Add(server.Id);
        try
        {
            var result = await StopUseCase.HandleAsync(server.Id, CancellationToken.None);
            if (!result.Succeeded)
                Snackbar.Add(result.Error!, Severity.Error);
            await LoadAsync();
        }
        finally
        {
            _busy.Remove(server.Id);
        }
    }

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;
        _servers = [.. await ServerRepository.ListByModpackAsync(ModpackId, CancellationToken.None)];

        // A coluna Status é cache; o Docker é a verdade. Sincroniza ao carregar,
        // para refletir paradas/crashes que aconteceram por fora do painel.
        foreach (var server in _servers)
        {
            var real = await Orchestrator.GetStatusAsync(server.Id, CancellationToken.None);
            if (real == server.Status) continue;

            server.Status = real;
            await ServerRepository.UpdateAsync(server, CancellationToken.None);
        }

        _versionsById = (await ModpackRepository.ListVersionsAsync(ModpackId, CancellationToken.None))
            .ToDictionary(v => v.Id);

        _modpack = await ModpackRepository.GetByIdAsync(ModpackId, CancellationToken.None);
        _isLoading = false;
    }

    private async Task ChangeVersion(GameServer server)
    {
        var parameters = new DialogParameters { ["Server"] = server };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<ChangeVersionDialog>("Versão do servidor", parameters, options);

        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task OpenForm(GameServer? existing)
    {
        var parameters = new DialogParameters { ["ModpackId"] = ModpackId, ["Existing"] = existing };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<ServerFormDialog>(
            existing is null ? "Novo servidor" : "Editar servidor", parameters, options);

        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task Delete(GameServer server)
    {
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar servidor",
            $"Apagar \"{server.Name}\"? O registro é removido do painel.",
            "Apagar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await DeleteUseCase.HandleAsync(server.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Servidor removido.", Severity.Success);
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }
}
