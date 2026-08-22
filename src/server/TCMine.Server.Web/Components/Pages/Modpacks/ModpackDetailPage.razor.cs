using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Background;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackDetailPage : ComponentBase, IDisposable
{
    private bool _isLoading = true;
    private bool _isPublishing;
    private bool _isRetrying;
    private Modpack? _modpack;
    private ModpackVersion? _selectedVersion;
    private Guid _selectedVersionId;
    private int _serverCount;

    [Parameter] public Guid ModpackId { get; set; }

    /// <summary>Contagens por versão, agregadas no banco (ver GetVersionStatsAsync).</summary>
    private IReadOnlyDictionary<Guid, ModpackVersionStats> _stats =
        new Dictionary<Guid, ModpackVersionStats>();

    private ModpackVersionStats SelectedStats =>
        _selectedVersionId != Guid.Empty && _stats.TryGetValue(_selectedVersionId, out var s)
            ? s
            : ModpackVersionStats.Empty;

    private int ModCount => SelectedStats.ModCount;
    private int OverrideCount => SelectedStats.OverrideCount;
    private long TotalSizeBytes => SelectedStats.TotalSizeBytes;

    /// <summary>Contagem de arquivos de uma versão qualquer (linha do tempo).</summary>
    private int FileCountOf(Guid versionId) =>
        _stats.TryGetValue(versionId, out var s) ? s.TotalCount : 0;

    /// <summary>
    ///     Quantos mods o pack de origem declara. Só existe em versão importada —
    ///     é o que permite mostrar "120 de 471" em vez de um spinner sem fim.
    ///     Calculado no load; desserializar o snapshot a cada render seria caro.
    /// </summary>
    private int? _expectedModCount;

    [Inject] private ISettingsRepository Settings { get; set; } = default!;
    [Inject] private PublishModpackVersion PublishUseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private DeleteModpackVersion DeleteVersionUseCase { get; set; } = default!;
    [Inject] private ArchiveModpackVersion ArchiveUseCase { get; set; } = default!;
    [Inject] private RestoreModpackVersion RestoreUseCase { get; set; } = default!;
    [Inject] private DeleteModpack DeleteModpackUseCase { get; set; } = default!;
    [Inject] private IServerRepository ServerRepository { get; set; } = default!;
    [Inject] private RetryModResolution RetryUseCase { get; set; } = default!;
    [Inject] private JobProgressRegistry Jobs { get; set; } = default!;
    [Inject] private CheckUpstreamUpdate UpstreamCheck { get; set; } = default!;

    /// <summary>Resultado da consulta à origem. Nulo enquanto não consultou (ou se falhou).</summary>
    private UpstreamUpdateStatus? _upstream;

    /// <summary>
    ///     Página do pack na origem. O CurseForge não expõe o slug no manifest,
    ///     mas /projects/{id} redireciona para a página certa — evita guardar uma
    ///     URL que envelhece se o autor renomear o pack.
    /// </summary>
    private string? UpstreamUrl => (_modpack?.UpstreamProvider, _modpack?.UpstreamProjectId) switch
    {
        (ModFileOrigin.CurseForge, { Length: > 0 } id) => $"https://www.curseforge.com/projects/{id}",
        (ModFileOrigin.Modrinth, { Length: > 0 } id) => $"https://modrinth.com/modpack/{id}",
        _ => null
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

    private async Task LoadAsync()
    {
        _isLoading = true;

        // GetByIdAsync traz o modpack com as versões, mas SEM os arquivos: num
        // pack importado são milhares de linhas que a tela não usa — só precisa
        // das contagens, que vêm agregadas do banco logo abaixo.
        _modpack = await Repository.GetByIdAsync(ModpackId, CancellationToken.None);
        _stats = await Repository.GetVersionStatsAsync(ModpackId, CancellationToken.None);

        if (_modpack is not null)
        {
            // Preserva a versão selecionada entre recargas (poll, publish); se ela
            // sumiu ou não havia seleção, cai na mais recente (lista por Id desc).
            var selected = _selectedVersionId != Guid.Empty
                ? _modpack.Versions.FirstOrDefault(v => v.Id == _selectedVersionId)
                : null;
            selected ??= _modpack.Versions.OrderByDescending(v => v.Id).FirstOrDefault();

            if (selected is not null)
            {
                _selectedVersionId = selected.Id;
                _selectedVersion = selected;
                _expectedModCount = UpstreamSnapshot.FromJson(selected.UpstreamSnapshotJson)?.Mods.Count;
            }

            _serverCount = (await ServerRepository.ListByModpackAsync(ModpackId, CancellationToken.None)).Count;
        }

        _isLoading = false;

        // Consulta à origem depois de pintar a tela: é rede, e prender o
        // carregamento da página por ela seria trocar um travamento por outro.
        _ = CheckUpstreamAsync();
    }

    private async Task CheckUpstreamAsync()
    {
        if (_selectedVersion?.UpstreamFileId is null)
        {
            _upstream = null;
            return;
        }

        var result = await UpstreamCheck.HandleAsync(_selectedVersionId, CancellationToken.None);

        // Falha aqui é silenciosa de propósito: a origem estar fora do ar não é
        // problema do admin que só queria ver a versão.
        _upstream = result.Succeeded ? result.Value : null;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Progresso empurrado pelo worker para a versão selecionada.</summary>
    private JobProgress? Progress =>
        _selectedVersionId == Guid.Empty ? null : Jobs.Get(_selectedVersionId);

    // Assina o registro de progresso: o worker avisa e a página se redesenha.
    // Antes isto era um Timer de 2s por circuito — progresso atrasado, grosseiro
    // (só mudava quando um mod inteiro terminava) e uma consulta por tique.
    private void OnJobChanged()
    {
        // Terminou: o estado da versão mudou no banco, então recarrega — é o
        // único momento em que vale reler.
        if (_selectedVersionId != Guid.Empty && Jobs.TryConsumeCompletion(_selectedVersionId, out var error))
        {
            _ = InvokeAsync(async () =>
            {
                await LoadAsync();
                if (error is { Length: > 0 })
                    Snackbar.Add(error, Severity.Error);
                StateHasChanged();
            });
            return;
        }

        // A versão entrou em Resolving depois que a página carregou (é o caso do
        // reparo): sem reler, o cabeçalho continuaria dizendo "rascunho" enquanto
        // a barra já mostra o download andando.
        if (_selectedVersion is { State: not ModpackVersionState.Resolving }
            && _selectedVersionId != Guid.Empty
            && Jobs.Get(_selectedVersionId) is not null)
        {
            _ = InvokeAsync(async () =>
            {
                await LoadAsync();
                StateHasChanged();
            });
            return;
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void OnVersionChanged(Guid versionId)
    {
        // Troca em memória: a Visão geral já tem todas as versões carregadas.
        _selectedVersionId = versionId;
        _selectedVersion = _modpack?.Versions.FirstOrDefault(v => v.Id == versionId);
    }

    private async Task OpenCreateVersion()
    {
        var latest = _modpack?.Versions.OrderByDescending(v => v.Id).FirstOrDefault();

        // A versão anterior manda mais que o padrão da instalação: se este pack
        // já rodou com 8 GB, repetir 4 GB do padrão seria um passo atrás. O
        // padrão só entra quando não há de quem herdar.
        var settings = await Settings.GetAsync(CancellationToken.None);
        var memoriaPadrao = latest?.RecommendedMemoryMb ?? settings.DefaultMemoryMb;

        var parameters = new DialogParameters
        {
            ["ModpackId"] = ModpackId,
            ["DefaultVersion"] = latest?.Version,
            ["MinecraftVersion"] = _modpack?.MinecraftVersion,
            ["Loader"] = _modpack?.Loader,
            ["DefaultLoaderVersion"] = latest?.LoaderVersion,
            ["DefaultMemoryMb"] = memoriaPadrao
        };

        var dialog = await DialogService.ShowAsync<CreateVersionDialog>("Nova versão", parameters);
        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task DeleteVersion()
    {
        if (_selectedVersion is null)
            return;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar rascunho",
            $"Apagar a versão {_selectedVersion.Version}? Os mods e overrides desta versão são removidos. Irreversível.",
            "Apagar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await DeleteVersionUseCase.HandleAsync(_selectedVersion.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Rascunho apagado.", Severity.Success);
            _selectedVersionId = Guid.Empty;
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private async Task Publish()
    {
        if (_selectedVersion is null)
            return;

        // Com pendências o texto muda: publicar assim entrega um pack a que
        // faltam mods, e o admin precisa ver isso antes de decidir.
        var pending = _selectedVersion.PendingMods;
        var message = pending.Count > 0
            ? $"A versão {_selectedVersion.Version} tem {pending.Count} mod(s) que não vieram: "
              + $"{string.Join(", ", pending.Take(5).Select(p => p.DisplayName))}"
              + (pending.Count > 5 ? "…" : "")
              + ". Quem instalar não terá esses mods. Publicar assim mesmo?"
            : $"Publicar a versão {_selectedVersion.Version}? A partir daqui ela fica imutável — "
              + "para mudanças, cria uma nova versão.";

        var confirm = await DialogService.ShowMessageBoxAsync(
            pending.Count > 0 ? "Publicar com mods faltando" : "Publicar versão",
            message,
            pending.Count > 0 ? "Publicar mesmo assim" : "Publicar",
            cancelText: "Cancelar");
        if (confirm is not true)
            return;

        _isPublishing = true;
        try
        {
            var result = await PublishUseCase.HandleAsync(
                _selectedVersion.Id, CancellationToken.None, acceptPending: pending.Count > 0);
            if (result.Succeeded)
            {
                Snackbar.Add("Versão publicada.", Severity.Success);
                await LoadAsync();
            }
            else
                Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            _isPublishing = false;
        }
    }

    private async Task UploadForPending(PendingMod pending)
    {
        var p = new DialogParameters
        {
            ["VersionId"] = _selectedVersion!.Id,
            ["ProjectSlug"] = pending.ProjectSlug,
            ["PendingName"] = pending.DisplayName
        };

        var dialog = await DialogService.ShowAsync<ManualUploadDialog>("Enviar arquivo", p);
        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task OpenUpstreamUpdate()
    {
        var p = new DialogParameters
        {
            ["VersionId"] = _selectedVersion!.Id,
            ["CurrentLabel"] = _selectedVersion.UpstreamVersionLabel ?? "",
            ["CurrentVersion"] = _selectedVersion.Version
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<UpstreamUpdateDialog>("Atualizar", p, options);

        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task OpenEditVersion()
    {
        var p = new DialogParameters
        {
            ["VersionId"] = _selectedVersion!.Id,
            ["Version"] = _selectedVersion.Version,
            ["LoaderVersion"] = _selectedVersion.LoaderVersion,
            ["MemoryMb"] = _selectedVersion.RecommendedMemoryMb,

            // O picker monta a lista a partir do par loader + versão do
            // Minecraft, que são do MODPACK: a versão não os carrega.
            ["Loader"] = _modpack!.Loader,
            ["MinecraftVersion"] = _modpack.MinecraftVersion
        };
        var dialog = await DialogService.ShowAsync<EditVersionDialog>("Editar versão", p);
        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task OpenCheckUpdates()
    {
        if (_selectedVersion is null)
            return;

        var parameters = new DialogParameters
        {
            ["SourceVersionId"] = _selectedVersion.Id, ["SourceVersion"] = _selectedVersion.Version
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<CheckUpdatesDialog>("Verificar atualizações", parameters, options);

        // Ok devolve o Id do Draft novo — leva o admin direto para os mods dele.
        if (await dialog.Result is { Canceled: false, Data: Guid newVersionId })
            Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{newVersionId}/mods");
    }

    // ---- Ações do modpack (não da versão) ----

    private async Task OpenEditModpack()
    {
        if (_modpack is null)
            return;

        var parameters = new DialogParameters
        {
            ["ModpackId"] = _modpack.Id,
            ["Name"] = _modpack.Name,
            ["Summary"] = _modpack.Summary,
            ["Slug"] = _modpack.Slug,
            ["MinecraftVersion"] = _modpack.MinecraftVersion,
            ["Loader"] = _modpack.Loader,
            ["IconUrl"] = _modpack.IconBlobSha256 is { } sha ? $"/api/v1/blobs/{sha}" : null
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<EditModpackDialog>("Editar modpack", parameters, options);
        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task DeleteModpackAsync()
    {
        if (_modpack is null)
            return;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar modpack",
            $"Apagar \"{_modpack.Name}\"? Todas as versões e arquivos serão removidos. Isto é irreversível.",
            "Apagar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await DeleteModpackUseCase.HandleAsync(_modpack.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Modpack apagado.", Severity.Success);
            // O modpack desta página deixou de existir; volta para o catálogo.
            Navigation.NavigateTo("/modpacks");
        }
        else
        {
            // A barreira dos servidores volta como mensagem clara aqui.
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }

    private async Task Archive()
    {
        if (_selectedVersion is null)
            return;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Arquivar versão",
            $"Arquivar a versão {_selectedVersion.Version}? Ela some de novas instalações, mas quem "
            + "já a usa continua rodando. Dá para restaurar depois.",
            "Arquivar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await ArchiveUseCase.HandleAsync(_selectedVersion.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Versão arquivada.", Severity.Success);
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private async Task Retry()
    {
        if (_selectedVersion is null || _isRetrying)
            return;

        _isRetrying = true;
        try
        {
            var result = await RetryUseCase.HandleAsync(_selectedVersion.Id, CancellationToken.None);
            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Error);
                return;
            }

            Snackbar.Add(
                result.Value > 0
                    ? $"Reparando: {result.Value} mods reenfileirados. O que já baixou foi mantido."
                    : "Versão devolvida para rascunho. Nada ficou faltando para rebaixar.",
                Severity.Success);

            // Não precisa sondar: a partir daqui o worker empurra o progresso.
            await LoadAsync();
        }
        finally
        {
            _isRetrying = false;
        }
    }

    private async Task Restore()
    {
        if (_selectedVersion is null)
            return;

        var result = await RestoreUseCase.HandleAsync(_selectedVersion.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Versão restaurada.", Severity.Success);
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    // ---- Card "próximo passo": severidade e textos conforme o estado ----

    private static Severity NextSeverity(ModpackVersionState state) => state switch
    {
        ModpackVersionState.Ready => Severity.Success,
        ModpackVersionState.Failed => Severity.Error,
        ModpackVersionState.Resolving => Severity.Info,
        ModpackVersionState.Archived => Severity.Normal,
        _ => Severity.Info
    };

    private static string NextTitle(ModpackVersionState state) => state switch
    {
        ModpackVersionState.Draft => "Rascunho editável",
        ModpackVersionState.Resolving => "Processando mods…",
        ModpackVersionState.Ready => "Versão publicada e imutável",
        ModpackVersionState.Failed => "A resolução falhou",
        ModpackVersionState.Archived => "Versão arquivada",
        _ => ""
    };

    private static string NextHint(ModpackVersionState state) => state switch
    {
        ModpackVersionState.Draft => "Adicione mods e overrides; publique quando estiver pronto.",
        ModpackVersionState.Resolving => "Baixando e verificando os arquivos. Isto atualiza sozinho.",
        ModpackVersionState.Ready => "Pronta para uso. Crie um servidor ou verifique se há mods mais novos.",
        ModpackVersionState.Failed => "Nada foi perdido: o reparo devolve a versão para rascunho e rebaixa só o que faltou.",
        ModpackVersionState.Archived => "Some de novas instalações, mas quem já a fixou continua rodando.",
        _ => ""
    };
}
