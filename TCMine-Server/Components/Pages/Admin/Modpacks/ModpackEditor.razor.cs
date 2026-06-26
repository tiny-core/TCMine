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

    // Aba ativa (controlada): a troca passa pelo OnTabPreview para mostrar o overlay antes do render
    private int _activeTab;
    private MudTabs _tabs = null!;
    private bool _switchingTab; // evita reentrância caso ActivatePanelAsync redispare o OnPreviewInteraction

    // Bundle de overrides pendente de um import (extraído só no Guardar); null = nada pendente
    private byte[]? _pendingOverrides;

    // Bumpado a cada Guardar: muda o @key do OverridesPanel, forçando-o a recarregar do disco
    private int _overridesVersion;

    // Estado do Guardar (com progresso de download dos jars)
    private bool _saving;
    private string _saveStatus = string.Empty;
    private int _saveCurrent;
    private int _saveTotal;

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
            }
            else
            {
                Nav.NavigateTo("/admin/modpacks");
                return;
            }

            _loading = false;
        });
    }

    // Intercepta a troca de aba: cancela a ativação nativa e a refaz sob o overlay, para que a modal
    // apareça ANTES do render pesado do painel (Mods/Overrides com muitos itens travam a thread de
    // render). Programático (mudar _activeTab) não dispara este preview de novo, então não há loop.
    private async Task OnTabPreview(TabInteractionEventArgs args)
    {
        if (args.InteractionType != TabInteractionType.Activate || _switchingTab || args.PanelIndex == _activeTab)
            return;

        args.Cancel = true; // nós conduzimos a troca, sob o overlay
        var target = args.PanelIndex;

        await Busy.RunAsync("Carregando aba…", async () =>
        {
            _activeTab = target;
            _switchingTab = true;
            try
            {
                await _tabs.ActivatePanelAsync(target, false);
            }
            finally
            {
                _switchingTab = false;
            }

            StateHasChanged();
            await Task.Yield(); // deixa o painel pesado renderizar com o overlay já visível
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
            await Service.SaveAsync(_draft, _mods, _pendingOverrides, progress);
            _pendingOverrides = null; // já extraído
            var wasNew = _isNew;
            _isNew = false;
            Snackbar.Add("Modpack guardado.", Severity.Success);

            // Recria o OverridesPanel para refletir o que o Save extraiu para o disco
            _overridesVersion++;

            if (wasNew)
                // Navega para a rota canônica do modpack já gravado (habilita overrides/novidades)
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
