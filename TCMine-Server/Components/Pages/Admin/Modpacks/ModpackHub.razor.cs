using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;
using TCMine_Server.Components.Pages.Admin.Servers.Dialogs;
using TCMine_Server.Services;
using TCMine_Infrastructure.Minecraft;
using TCMine_Infrastructure.ServerInstances;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Hub (overview) de um modpack: substitui a página de abas como landing. Mostra o resumo + cartões de
/// cada área e, sobretudo, as <b>instâncias de servidor derivadas</b> deste modpack (a ligação que faltava)
/// — com selo de "desatualizada" e ações de ciclo de vida. Metadados editam num modal; as áreas pesadas
/// (Mods/Overrides/Novidades/Conexões) abrem no editor completo (a Fase 3 as torna páginas próprias).
/// </summary>
public partial class ModpackHub : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private ModpackImportService Packs { get; set; } = null!;
    [Inject] private ServerInstanceService Servers { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private ModpackAdminRowDto? _pack;
    private List<ServerInstanceRowDto>? _instances;

    protected override async Task OnParametersSetAsync()
    {
        await Busy.RunAsync("Carregando modpack…", LoadAsync);
    }

    private async Task LoadAsync()
    {
        // Resumo leve (projeção com contagens). Filtra a linha deste modpack.
        _pack = (await Packs.ListAsync()).FirstOrDefault(p => p.Id == Id);
        if (_pack is null)
        {
            Nav.NavigateTo("/admin/modpacks");
            return;
        }

        _instances = await Servers.ListByModpackAsync(Id);
    }

    private async Task ReloadInstancesAsync()
    {
        _instances = await Servers.ListByModpackAsync(Id);
    }

    // Novidades em modal (NewsPanel grava sozinho); ao fechar, recarrega o resumo
    private async Task OpenNewsAsync()
    {
        var parameters = new DialogParameters<ModpackNewsDialog> { { x => x.ModpackId, Id } };
        var dialog = await DialogService.ShowAsync<ModpackNewsDialog>("Novidades do modpack", parameters, Wide());
        await dialog.Result;
        await LoadAsync();
    }

    // Conexões divulgadas (manuais) em modal; persiste o conjunto editado e recarrega o resumo
    private async Task OpenConnectionsAsync()
    {
        var parameters = new DialogParameters<ModpackConnectionsDialog> { { x => x.ModpackId, Id } };
        var dialog = await DialogService.ShowAsync<ModpackConnectionsDialog>("Conexões divulgadas", parameters, Wide());
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not List<ServerEntryEntity> entries) return;

        try
        {
            await Busy.RunAsync("Salvando conexões…", async () =>
            {
                await Packs.SaveConnectionsAsync(Id, entries);
                await LoadAsync();
            });
            Snackbar.Add("Conexões atualizadas.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    // ── Detalhes (modal de metadados) ─────────────────────────────────────────────────────────────────

    private async Task EditDetailsAsync()
    {
        // Carrega o modpack completo e clona só os metadados — o modal edita o clone (cancelar não vaza)
        var full = await Packs.GetForEditAsync(Id);
        if (full is null) return;

        var draft = new ModpackEntity
        {
            Id = full.Id, Name = full.Name, Version = full.Version, Minecraft = full.Minecraft,
            Loader = full.Loader, LoaderVersion = full.LoaderVersion, Description = full.Description,
            IsPublished = full.IsPublished, RecommendedRamMb = full.RecommendedRamMb,
            HasOverrides = full.HasOverrides, UpdatedAt = full.UpdatedAt
        };

        var parameters = new DialogParameters<ModpackDetailsDialog> { { x => x.Draft, draft } };
        var dialog = await DialogService.ShowAsync<ModpackDetailsDialog>("Detalhes do modpack", parameters, Wide());
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not ModpackEntity edited) return;

        try
        {
            await Busy.RunAsync("Salvando detalhes…", async () =>
            {
                await Packs.UpdateMetadataAsync(edited);
                await LoadAsync();
            });
            Snackbar.Add("Detalhes atualizados.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    // ── Instâncias deste modpack ──────────────────────────────────────────────────────────────────────

    private async Task CreateInstanceAsync()
    {
        var parameters = new DialogParameters<ServerInstanceEditDialog> { { x => x.PresetModpackId, Id } };
        var dialog = await DialogService.ShowAsync<ServerInstanceEditDialog>(
            "Novo servidor deste modpack", parameters, Wide());
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not ServerInstanceEditDto dto) return;

        try
        {
            await Busy.RunAsync("Criando instância…", async () =>
            {
                await Servers.CreateAsync(dto);
                await ReloadInstancesAsync();
            });
            Snackbar.Add($"Instância \"{dto.Name}\" criada. Provisione-a para montar o diretório.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    // Provisiona / aplica atualização (mesma operação: re-monta o diretório a partir do modpack atual)
    private async Task ProvisionAsync(ServerInstanceRowDto row)
    {
        await RunInstance(row.IsStale ? "Aplicando atualização…" : "Provisionando…",
            () => Servers.ProvisionAsync(row.Id, Busy.Progress()),
            row.IsStale ? "Atualização aplicada." : "Instância provisionada.");
    }

    private Task StartAsync(ServerInstanceRowDto row)
    {
        return RunInstance("Iniciando servidor…", () => Servers.StartAsync(row.Id), "Servidor iniciando.");
    }

    private Task StopAsync(ServerInstanceRowDto row)
    {
        return RunInstance("Parando servidor…", () => Servers.StopAsync(row.Id), "Servidor parado.");
    }

    private async Task RunInstance(string message, Func<Task> operation, string success)
    {
        try
        {
            await Busy.RunAsync(message, async () =>
            {
                await operation();
                await ReloadInstancesAsync();
            });
            Snackbar.Add(success, Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private void OpenInstance(ServerInstanceRowDto row)
    {
        Nav.NavigateTo($"/admin/servers/{row.Id}");
    }

    private static DialogOptions Wide()
    {
        return new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
    }

    // ── Apresentação do status da instância ─────────────────────────────────────────────────────────────

    private static string StatusLabel(ServerInstanceStatus s)
    {
        return s switch
        {
            ServerInstanceStatus.Running => "Em execução",
            ServerInstanceStatus.Starting => "Iniciando",
            ServerInstanceStatus.Stopping => "Parando",
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
            ServerInstanceStatus.Crashed => Color.Error,
            _ => Color.Default
        };
    }
}
