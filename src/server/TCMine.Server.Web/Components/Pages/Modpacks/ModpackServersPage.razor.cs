using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackServersPage
{
    private List<BreadcrumbItem> _breadcrumbs = [];

    private bool _isLoading = true;
    private List<GameServer> _servers = [];

    private Dictionary<Guid, ModpackVersion> _versionsById = new();
    [Parameter] public Guid ModpackId { get; set; }

    [Inject] private IServerRepository ServerRepository { get; set; } = default!;
    [Inject] private DeleteGameServer DeleteUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Inject] private IModpackRepository ModpackRepository { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _servers = [.. await ServerRepository.ListByModpackAsync(ModpackId, CancellationToken.None)];
        _versionsById = (await ModpackRepository.ListVersionsAsync(ModpackId, CancellationToken.None))
            .ToDictionary(v => v.Id);
        _breadcrumbs =
        [
            new BreadcrumbItem("Modpacks", "/modpacks"),
            new BreadcrumbItem("Modpack", $"/modpacks/{ModpackId}"),
            new BreadcrumbItem("Servidores", null, true)
        ];
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
        var parameters = new DialogParameters
        {
            ["ModpackId"] = ModpackId,
            ["Existing"] = existing
        };
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
        {
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }
}