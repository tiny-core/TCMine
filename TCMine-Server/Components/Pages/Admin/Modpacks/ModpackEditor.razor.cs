using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Domain.Modpack;
using TCMine_Infrastructure.Minecraft;
using TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Editor de um modpack (criar ou editar). **Orquestra** as abas — cada uma é um componente próprio
/// (<see cref="DetailsPanel"/>, <see cref="ModsPanel"/>, <see cref="OverridesPanel"/>,
/// <see cref="NewsPanel"/>, <see cref="ServersPanel"/>). Mantém o rascunho destacado (<see cref="_draft"/>
/// + <see cref="_mods"/>) e a política de escrita-só-ao-Guardar para metadados, mods e servidores.
///
/// Exceção: overrides e novidades gravam direto no disco/banco (têm seus próprios serviços), por isso
/// só ficam disponíveis depois do primeiro Guardar — antes disso o modpack ainda não existe.
/// </summary>
public partial class ModpackEditor : ComponentBase
{
    [Parameter] public string? Id { get; set; }

    [Inject] private ModpackImportService Service { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    // Rascunho destacado: a fonte da verdade do formulário até o Guardar
    private ModpackEntity _draft = new();

    // Mods do rascunho como lista plana (campos do arquivo + Side/Target por-modpack). A persistência
    // decompõe isto em ModFile (compartilhado) + ModpackMod (junção) no SaveAsync.
    private List<ModEntryEntity> _mods = [];

    private bool _isNew;
    private bool _loading = true;
    private bool _cfConfigured;

    // Bundle de overrides pendente de um import (extraído só no Guardar); null = nada pendente
    private byte[]? _pendingOverrides;

    // Origem CF de um import feito nesta sessão (persistida no Guardar); null = nada novo
    private ModpackImportSourceEntity? _pendingSource;

    // Origem CF já gravada (alimenta o banner de "verificar atualização"); null = não veio do CF
    private ModpackImportSourceEntity? _importSource;

    // Estado do Guardar (com progresso de download dos jars)
    private bool _saving;
    private string _saveStatus = string.Empty;
    private int _saveCurrent;
    private int _saveTotal;

    // Voltar: na criação volta para a lista; editando mods, volta para o hub do modpack
    private string BackHref => _isNew ? "/admin/modpacks" : $"/admin/modpacks/{_draft.Id}";

    protected override async Task OnParametersSetAsync()
    {
        await Busy.RunAsync("Carregando modpack…", async () =>
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
                _mods = [];
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
                // Achata os vínculos+arquivos no modelo plano que o editor edita
                _mods = ModpackImportService.FlattenMods(existing);
                // Origem CF (se houver) — para o banner de atualização. Não chama a API no load.
                _importSource = await Service.GetImportSourceAsync(uid);
            }
            else
            {
                Nav.NavigateTo("/admin/modpacks");
                return;
            }

            _loading = false;
        });
    }

    // ── Mods (ações disparadas pelo ModsPanel) ───────────────────────────────────────────────────

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
        await Busy.RunAsync("Adicionando mods…", async () =>
        {
            foreach (var modId in modIds)
                try
                {
                    var entry = await Service.AddFromSearchAsync(modId, _draft.Minecraft, _draft.Loader);
                    if (MergeMod(entry)) added++;
                }
                catch (Exception ex)
                {
                    Snackbar.Add($"Falha ao adicionar mod {modId}: {ex.Message}", Severity.Error);
                }
        });

        if (added > 0) Snackbar.Add($"{added} mod(s) adicionado(s).", Severity.Success);
    }

    private async Task ImportModpackAsync()
    {
        var dialog = await DialogService.ShowAsync<ImportModpackDialog>(
            "Importar modpack do CurseForge", new DialogParameters(), WideDialog());

        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not long projectId) return;

        await RunImportAsync(projectId, isUpdate: false);
    }

    // Import (ou atualização) de um modpack CF para o rascunho. Mescla os mods preservando Side/Target,
    // registra a origem (_pendingSource) e deixa os overrides pendentes para o Guardar.
    private async Task RunImportAsync(long projectId, bool isUpdate)
    {
        // Estado observável: o diálogo reflete cada fase do import ao vivo (em vez de texto fixo)
        var progressState = new ProgressState("Iniciando import…");
        var progress = new Progress<string>(progressState.Report);

        // Modal de feedback bloqueante: impede o usuário de mexer no editor durante o import
        var progressDialog = await DialogService.ShowAsync<ImportProgressDialog>(
            isUpdate ? "Atualizando modpack" : "Importando modpack",
            new DialogParameters<ImportProgressDialog> { { x => x.State, progressState } }, BlockingDialog());

        try
        {
            var imported = await Service.ImportModpackToDraftAsync(projectId, progress);

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

            // Registra a origem CF (versão importada) — persistida no Guardar
            _pendingSource = new ModpackImportSourceEntity
            {
                CurseProjectId = imported.CurseProjectId,
                CurseProjectName = imported.Name,
                InstalledFileId = imported.CurseFileId,
                InstalledVersion = imported.Version
            };

            Snackbar.Add(
                $"{(isUpdate ? "Atualizado" : "Importado")}: {added} mod(s) novo(s)." +
                (_pendingOverrides is not null ? " Overrides serão extraídos ao Guardar." : "") +
                " Clique em Guardar para aplicar.",
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

    // ── Atualizações (CurseForge) ────────────────────────────────────────────────────────────────

    // Botão "Buscar atualizações" da aba Mods: 1 batch ao CF, lista as novidades e aplica as escolhidas.
    private async Task CheckModUpdatesAsync()
    {
        List<ModUpdateDto> updates = [];
        try
        {
            await Busy.RunAsync("Buscando atualizações…", async () =>
            {
                updates = await Service.CheckModUpdatesAsync(_mods, _draft.Minecraft, _draft.Loader);
            });
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao buscar atualizações: {ex.Message}", Severity.Error);
            return;
        }

        if (updates.Count == 0)
        {
            Snackbar.Add("Todos os mods estão atualizados.", Severity.Info);
            return;
        }

        var parameters = new DialogParameters<ModUpdatesDialog> { { x => x.Updates, updates } };
        var dialog = await DialogService.ShowAsync<ModUpdatesDialog>("Atualizações de mods", parameters, WideDialog());
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not List<ModUpdateDto> chosen || chosen.Count == 0)
            return;

        foreach (var u in chosen)
            ApplyModUpdate(u);

        Snackbar.Add($"{chosen.Count} mod(s) atualizado(s) no rascunho. Guarde para baixar.", Severity.Success);
    }

    // Aplica uma atualização no rascunho: troca arquivo/versão/url e zera o hash (Save re-baixa o jar).
    private void ApplyModUpdate(ModUpdateDto update)
    {
        var mod = _mods.FirstOrDefault(m => m.CurseModId == update.CurseModId);
        if (mod is null) return;

        mod.FileId = update.LatestFileId;
        mod.Version = update.LatestVersion;
        mod.FileName = update.FileName;
        mod.DownloadUrl = update.DownloadUrl;
        mod.Sha1 = null; // força o re-download no Guardar
        mod.FileLength = 0;
    }

    // Banner: verifica se o modpack importado tem versão nova (force = ignora o TTL do cache)
    private async Task CheckModpackUpdateAsync()
    {
        if (_importSource is null) return;
        try
        {
            await Busy.RunAsync("Verificando atualização do modpack…", async () =>
            {
                var status = await Service.CheckModpackUpdateAsync(_draft.Id, force: true);
                if (status is not null) _importSource = await Service.GetImportSourceAsync(_draft.Id);
            });

            Snackbar.Add(
                _importSource?.UpdateAvailable == true
                    ? $"Atualização disponível: {_importSource.LatestVersion}."
                    : "O modpack está na versão mais recente.",
                _importSource?.UpdateAvailable == true ? Severity.Info : Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao verificar: {ex.Message}", Severity.Error);
        }
    }

    // Botão "Atualizar modpack": re-importa a versão mais recente e mescla no rascunho
    private async Task UpdateModpackAsync()
    {
        if (_importSource is null) return;
        await RunImportAsync(_importSource.CurseProjectId, isUpdate: true);
    }

    private async Task UploadJarAsync(IBrowserFile file)
    {
        try
        {
            await Busy.RunAsync("Enviando arquivo…", async () =>
            {
                // 50 MB de teto por jar — generoso, mas evita esgotar memória num upload acidental
                await using var stream = file.OpenReadStream(50 * 1024 * 1024);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                var entry = await Service.AddUploadedModAsync(file.Name, ms.ToArray());
                MergeMod(entry);
                Snackbar.Add($"\"{entry.FileName}\" enviado.", Severity.Success);
            });
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha no upload: {ex.Message}", Severity.Error);
        }
    }

    // Mescla um mod no rascunho por FileId (mesma versão = atualiza; novo = adiciona). True se adicionou.
    private bool MergeMod(ModEntryEntity entry)
    {
        var existing = _mods.FirstOrDefault(m => m.FileId == entry.FileId);
        if (existing is not null)
        {
            // Preserva o Side/Target já ajustado pelo admin; só atualiza o que veio resolvido
            existing.Name = entry.Name;
            existing.Version = entry.Version;
            existing.FileName = entry.FileName;
            existing.DownloadUrl = entry.DownloadUrl;
            return false;
        }

        _mods.Add(entry);
        return true;
    }

    private void RemoveMod(ModEntryEntity mod)
    {
        _mods.Remove(mod);
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
            await Service.SaveAsync(_draft, _mods, _pendingOverrides, _pendingSource, progress);
            _pendingOverrides = null; // já extraído
            _pendingSource = null; // já persistido
            var wasNew = _isNew;
            _isNew = false;
            Snackbar.Add("Modpack guardado.", Severity.Success);

            // Recarrega a origem gravada (atualiza o banner; a versão instalada pode ter mudado)
            if (!wasNew) _importSource = await Service.GetImportSourceAsync(_draft.Id);

            if (wasNew)
                // Recém-criado: vai para o hub do modpack (overrides/novidades/instâncias)
                Nav.NavigateTo($"/admin/modpacks/{_draft.Id}");
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
