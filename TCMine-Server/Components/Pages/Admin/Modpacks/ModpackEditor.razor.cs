using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Domain.Modpack;
using TCMine_Infrastructure.Minecraft;
using TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Editor de um modpack (criar ou editar). Segue a política de escrita-só-ao-Guardar para os
/// metadados, mods e servidores: tudo vive num rascunho destacado (<see cref="_draft"/>) em memória
/// e só o botão Guardar persiste (baixando os jars com progresso).
///
/// Exceção deliberada: a edição de <b>overrides</b> grava direto em disco (o
/// <see cref="ModpackImportService"/> já trabalha assim, com histórico/desfazer). Por isso a aba de
/// overrides só fica disponível depois do primeiro Guardar — antes disso o modpack ainda não tem
/// pasta no disco e os overrides de um import ficam pendentes até serem extraídos no Guardar.
/// </summary>
public partial class ModpackEditor : ComponentBase
{
    [Parameter] public string? Id { get; set; }

    [Inject] private ModpackImportService Service { get; set; } = null!;
    [Inject] private MinecraftVersionService Versions { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    // Rascunho destacado: a fonte da verdade do formulário até o Guardar
    private ModpackEntity _draft = new();
    private bool _isNew;
    private bool _loading = true;
    private bool _cfConfigured;

    // Bundle de overrides pendente de um import (extraído só no Guardar); null = nada pendente
    private byte[]? _pendingOverrides;

    // Bumpado a cada Guardar: muda o @key do OverridesPanel, forçando-o a recarregar do disco
    private int _overridesVersion;

    // Estado do Guardar (com progresso de download dos jars)
    private bool _saving;
    private string _saveStatus = string.Empty;
    private int _saveCurrent;
    private int _saveTotal;

    // Seletores de versão (preenchidos por endpoints oficiais; podem vir vazios → texto livre)
    private IReadOnlyList<VersionOptionDto> _mcVersions = [];
    private IReadOnlyList<VersionOptionDto> _loaderVersions = [];

    private bool Exists => !_isNew;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _cfConfigured = await Service.IsCfConfiguredAsync();

        if (string.IsNullOrEmpty(Id) || Id.Equals("new", StringComparison.OrdinalIgnoreCase))
        {
            _isNew = true;
            _draft = new ModpackEntity
            {
                Id = Guid.NewGuid(),
                Name = string.Empty,
                Version = "1.0.0",
                Loader = ModLoader.NeoForge,
                IsPublished = false
            };
        }
        else if (Guid.TryParse(Id, out var uid))
        {
            var existing = await Service.GetForEditAsync(uid);
            if (existing is null)
            {
                Nav.NavigateTo("/admin/modpacks");
                return;
            }

            _isNew = false;
            _draft = existing;
            await ReloadNewsAsync();
        }
        else
        {
            Nav.NavigateTo("/admin/modpacks");
            return;
        }

        _mcVersions = await Versions.GetMinecraftVersionsAsync();
        await ReloadLoaderVersionsAsync();
        _loading = false;
    }

    // ── Versões ────────────────────────────────────────────────────────────────────────────────

    private async Task OnMinecraftChanged(string value)
    {
        _draft.Minecraft = value;
        await ReloadLoaderVersionsAsync();
    }

    private async Task OnLoaderChanged(ModLoader value)
    {
        _draft.Loader = value;
        await ReloadLoaderVersionsAsync();
    }

    private async Task ReloadLoaderVersionsAsync()
    {
        _loaderVersions = await Versions.GetLoaderVersionsAsync(_draft.Loader, _draft.Minecraft);
    }

    // Destinos possíveis de um arquivo no cliente (pasta de instalação)
    private static readonly string[] Targets = ["mod", "resourcepack", "shaderpack"];

    // Mostrar só lançamentos estáveis (oculta snapshots/beta/rc) — ligado por padrão
    private bool _mcReleasesOnly = true;
    private bool _loaderReleasesOnly = true;

    // SearchFunc do MudAutocomplete: filtra as versões e devolve texto livre quando não há lista
    private Task<IEnumerable<string>> SearchMcAsync(string value, CancellationToken ct)
    {
        return Task.FromResult(Filter(_mcVersions, value, _mcReleasesOnly));
    }

    private Task<IEnumerable<string>> SearchLoaderAsync(string value, CancellationToken ct)
    {
        return Task.FromResult(Filter(_loaderVersions, value, _loaderReleasesOnly));
    }

    private static IEnumerable<string> Filter(IReadOnlyList<VersionOptionDto> opts, string? value, bool releasesOnly)
    {
        IEnumerable<VersionOptionDto> q = opts;
        if (releasesOnly)
            q = q.Where(o => o.IsRelease);
        if (!string.IsNullOrWhiteSpace(value))
            q = q.Where(o => o.Version.Contains(value, StringComparison.OrdinalIgnoreCase));
        return q.Take(100).Select(o => o.Version);
    }

    // ── Mods ───────────────────────────────────────────────────────────────────────────────────

    private async Task SearchModsAsync()
    {
        var parameters = new DialogParameters<CurseForgeSearchDialog>
        {
            { x => x.GameVersion, _draft.Minecraft },
            { x => x.Loader, _draft.Loader }
        };
        var dialog = await DialogService.ShowAsync<CurseForgeSearchDialog>(
            "Buscar mods no CurseForge", parameters, WideDialog());

        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not List<long> modIds || modIds.Count == 0)
            return;

        var added = 0;
        foreach (var modId in modIds)
        {
            try
            {
                var entry = await Service.AddFromSearchAsync(modId, _draft.Minecraft, _draft.Loader);
                if (MergeMod(entry)) added++;
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Falha ao adicionar mod {modId}: {ex.Message}", Severity.Error);
            }
        }

        if (added > 0) Snackbar.Add($"{added} mod(s) adicionado(s).", Severity.Success);
    }

    private async Task ImportModpackAsync()
    {
        var dialog = await DialogService.ShowAsync<ImportModpackDialog>(
            "Importar modpack do CurseForge", new DialogParameters(), WideDialog());

        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not long projectId) return;

        // Modal de feedback bloqueante: impede o usuário de mexer no editor durante o import
        var progressDialog = await DialogService.ShowAsync<ImportProgressDialog>(
            "Importando modpack", new DialogParameters(), BlockingDialog());

        try
        {
            var imported = await Service.ImportModpackToDraftAsync(projectId);

            // Metadados só preenchem campos ainda vazios — não sobrescrevem o que o admin já definiu
            if (string.IsNullOrWhiteSpace(_draft.Name)) _draft.Name = imported.Name;
            _draft.Version = imported.Version;
            _draft.Minecraft = imported.Minecraft;
            _draft.Loader = imported.Loader;
            _draft.LoaderVersion = imported.LoaderVersion;

            var added = 0;
            foreach (var mod in imported.Mods)
                if (MergeMod(mod))
                    added++;

            // Overrides do import ficam pendentes até o Guardar extrair para o disco
            if (imported.Overrides is { Length: > 0 })
                _pendingOverrides = imported.Overrides;

            await ReloadLoaderVersionsAsync();
            Snackbar.Add(
                $"Importado: {added} mod(s) novo(s)." +
                (_pendingOverrides is not null ? " Overrides serão extraídos ao Guardar." : ""),
                Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao importar: {ex.Message}", Severity.Error);
        }
        finally
        {
            progressDialog.Close();
        }
    }

    private async Task UploadJarAsync(IBrowserFile file)
    {
        try
        {
            // 50 MB de teto por jar — generoso, mas evita esgotar memória num upload acidental
            await using var stream = file.OpenReadStream(50 * 1024 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            var entry = await Service.AddUploadedModAsync(file.Name, ms.ToArray());
            MergeMod(entry);
            Snackbar.Add($"\"{entry.FileName}\" enviado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha no upload: {ex.Message}", Severity.Error);
        }
    }

    // Mescla um mod no rascunho por FileId (mesma versão = atualiza; novo = adiciona). Devolve true se adicionou.
    private bool MergeMod(ModEntryEntity entry)
    {
        var existing = _draft.Mods.FirstOrDefault(m => m.FileId == entry.FileId);
        if (existing is not null)
        {
            // Preserva o Side/Target já ajustado pelo admin; só atualiza o que veio resolvido
            existing.Name = entry.Name;
            existing.Version = entry.Version;
            existing.FileName = entry.FileName;
            existing.DownloadUrl = entry.DownloadUrl;
            return false;
        }

        _draft.Mods.Add(entry);
        return true;
    }

    private void RemoveMod(ModEntryEntity mod)
    {
        _draft.Mods.Remove(mod);
    }

    // ── Servidores ───────────────────────────────────────────────────────────────────────────────

    private void AddServer()
    {
        _draft.Servers.Add(new ServerEntryEntity { Name = "Novo servidor", Address = "", Port = 25565 });
    }

    private void RemoveServer(ServerEntryEntity server)
    {
        _draft.Servers.Remove(server);
    }

    // ── Guardar ────────────────────────────────────────────────────────────────────────────────

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_draft.Name))
        {
            Snackbar.Add("Dê um nome ao modpack.", Severity.Warning);
            return;
        }

        _saving = true;
        _saveCurrent = 0;
        _saveTotal = 0;
        _saveStatus = "Preparando…";

        var progress = new Progress<SaveProgressDto>(p =>
        {
            _saveCurrent = p.Current;
            _saveTotal = p.Total;
            _saveStatus = p.Total > 0 ? $"Baixando {p.FileName} ({p.Current}/{p.Total})" : p.FileName;
            InvokeAsync(StateHasChanged);
        });

        try
        {
            await Service.SaveAsync(_draft, _pendingOverrides, progress);
            _pendingOverrides = null; // já extraído
            var wasNew = _isNew;
            _isNew = false;
            Snackbar.Add("Modpack guardado.", Severity.Success);

            // Recria o OverridesPanel para refletir o que o Save extraiu para o disco
            _overridesVersion++;

            if (wasNew)
            {
                // Navega para a rota canônica do modpack já gravado (habilita a aba de overrides)
                Nav.NavigateTo($"/admin/modpacks/{_draft.Id}");
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao guardar: {ex.Message}", Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private static DialogOptions WideDialog()
    {
        return new DialogOptions
        {
            MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true,
            BackdropClick = false
        };
    }

    // Modal bloqueante: sem botão de fechar, sem ESC, sem clique no backdrop — só fecha por código
    private static DialogOptions BlockingDialog()
    {
        return new DialogOptions
        {
            MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseButton = false,
            BackdropClick = false, CloseOnEscapeKey = false
        };
    }
}
