using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Dns;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Components.Pages;

public partial class SettingsPage : ComponentBase
{
    /// <summary>
    ///     Client ID gravado no painel. Ao contrário dos segredos desta tela, ele
    ///     volta preenchido: é público, o launcher o recebe de qualquer forma no
    ///     handshake, e escondê-lo só impediria o admin de conferir o que está no ar.
    /// </summary>
    private string _azureClientId = "";

    /// <summary>O que veio de appsettings, só para explicar de onde sai o valor em uso.</summary>
    private string _azureClientIdFromFile = "";

    private bool _clearCurseForgeKey;

    // Novos valores digitados. Vazio = manter o que já está gravado.
    private string _curseForgeKey = "";
    private ModLoader _defaultLoader = ModLoader.NeoForge;
    private string _defaultMcVersion = "";
    private int _defaultMemoryMb = 4096;
    private int _gamePortRangeEnd = GamePortDefaults.Last;
    private int _gamePortRangeStart = GamePortDefaults.First;

    private bool _hasAzureClientIdFromFile;

    /// <summary>Só sabemos se existe — o valor nunca volta para a tela.</summary>
    private bool _hasCurseForgeKey;

    /// <summary>IP público detectado. Nulo enquanto detecta e quando não se detectou.</summary>
    private string? _detectedAddress;

    /// <summary>Começa ligado: a detecção só roda depois do primeiro render.</summary>
    private bool _isDetecting = true;

    // ---- DNS (Cloudflare) ----
    private bool _clearCloudflareToken;

    /// <summary>Novo token digitado. Vazio = manter o que já está gravado.</summary>
    private string _cloudflareToken = "";

    private string _cloudflareZoneId = "";
    private string _dnsBaseDomain = "";
    private string _dnsHostLabel = "";

    /// <summary>Resultado da última sincronização pedida nesta tela. Nulo = ainda não pediu.</summary>
    private DnsSyncReport? _dnsReport;

    /// <summary>Só sabemos se existe — o token nunca volta para a tela.</summary>
    private bool _hasCloudflareToken;

    private bool _isSyncingDns;

    private bool _isLoading = true;
    private bool _isSaving;

    /// <summary>Volta preenchido, como o client ID: é público, e o admin precisa conferi-lo.</summary>
    private string _publicHost = "";

    private int _worldBackupKeepCount = 5;

    /// <summary>
    ///     O que mostrar no URI do broker enquanto ninguém digitou nada. Um
    ///     placeholder evita que o admin copie <c>.../brokerplugin/</c> truncado
    ///     para o Azure e passe a caçar um erro de login que nasceu aqui.
    /// </summary>
    private string _azureClientIdPreview =>
        string.IsNullOrWhiteSpace(_azureClientId)
            ? "{client-id}"
            : _azureClientId.Trim();

    /// <summary>
    ///     O terceiro URI, o do login do PAINEL — diferente dos outros dois, este
    ///     não depende do client id, só do domínio onde este TCMine está servindo
    ///     agora. BaseUri já vem com a porta certa em dev e o domínio certo atrás
    ///     de proxy, então não há nada a calcular além de trocar a barra final
    ///     pelo caminho do callback.
    /// </summary>
    private string _microsoftCallbackUrl => $"{Navigation.BaseUri.TrimEnd('/')}/auth/microsoft/callback";

    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISettingsRepository Repository { get; set; } = default!;
    [Inject] private IOptions<ServerOptions> ServerOptions { get; set; } = default!;
    [Inject] private UpdateSettings UpdateUseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private GetAddressSettings AddressLookup { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private SyncGameDns DnsSync { get; set; } = default!;

    /// <summary>
    ///     O nome do registro A como ficará, para o admin ver o que vai aparecer
    ///     na zona dele antes de salvar.
    /// </summary>
    private string DnsHostPreview =>
        GameDns.ServerHost(
            string.IsNullOrWhiteSpace(_dnsHostLabel) ? GameDns.DefaultHostLabel : _dnsHostLabel,
            string.IsNullOrWhiteSpace(_dnsBaseDomain) ? "seu-dominio.com" : _dnsBaseDomain)!;

    /// <summary>
    ///     Sincroniza com o que está GRAVADO, não com o que está digitado: o caso
    ///     de uso lê a configuração do banco, como fará quando rodar sozinho. Por
    ///     isso o botão avisa para salvar antes.
    /// </summary>
    private async Task SyncDnsAsync()
    {
        _isSyncingDns = true;
        try
        {
            _dnsReport = await DnsSync.HandleAsync(CancellationToken.None);
        }
        finally
        {
            _isSyncingDns = false;
        }
    }

    protected override Task OnInitializedAsync() => LoadAsync();

    /// <summary>
    ///     A detecção do IP sai para a rede, e por isso fica FORA do carregamento
    ///     da página: aqui ela roda com a tela já desenhada, e uma rede lenta
    ///     custa um spinner num canto em vez de segurar as configurações inteiras.
    ///     Também não roda no pré-render, que esperaria por ela antes de responder.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        try
        {
            _detectedAddress = (await AddressLookup.HandleAsync(true, CancellationToken.None)).DetectedHost;
        }
        finally
        {
            _isDetecting = false;
            StateHasChanged();
        }
    }

    private async Task CopyDetectedAddress()
    {
        if (_detectedAddress is null)
            return;

        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", _detectedAddress);
        Snackbar.Add("IP copiado.", Severity.Info);
    }

    private async Task LoadAsync()
    {
        _isLoading = true;

        var settings = await Repository.GetAsync(CancellationToken.None);

        _defaultMcVersion = settings.DefaultMinecraftVersion ?? "";
        _defaultLoader = settings.DefaultLoader;
        _defaultMemoryMb = settings.DefaultMemoryMb;
        _worldBackupKeepCount = settings.WorldBackupKeepCount;

        // Normalizada: uma faixa inválida no banco aparece aqui como a padrão,
        // que é a que o alocador está usando de fato.
        (_gamePortRangeStart, _gamePortRangeEnd) =
            GamePortRange.Normalize(settings.GamePortRangeStart, settings.GamePortRangeEnd);

        _publicHost = settings.PublicHost ?? "";

        _hasCloudflareToken = !string.IsNullOrEmpty(settings.CloudflareApiTokenEncrypted);
        _cloudflareZoneId = settings.CloudflareZoneId ?? "";
        _dnsBaseDomain = settings.DnsBaseDomain ?? "";
        _dnsHostLabel = settings.DnsHostLabel ?? "";
        _cloudflareToken = "";
        _clearCloudflareToken = false;

        // Guardamos só a existência; o segredo em si não vai para a UI.
        _hasCurseForgeKey = !string.IsNullOrEmpty(settings.CurseForgeApiKeyEncrypted);

        _azureClientId = settings.AzureClientId ?? "";
        _azureClientIdFromFile = ServerOptions.Value.AzureClientId;
        _hasAzureClientIdFromFile = !string.IsNullOrWhiteSpace(_azureClientIdFromFile);

        _curseForgeKey = "";
        _clearCurseForgeKey = false;

        _isLoading = false;
    }

    private async Task Save()
    {
        _isSaving = true;
        try
        {
            var command = new UpdateSettingsCommand
            {
                DefaultMinecraftVersion = _defaultMcVersion,
                DefaultLoader = _defaultLoader,
                DefaultMemoryMb = _defaultMemoryMb,
                WorldBackupKeepCount = _worldBackupKeepCount,
                GamePortRangeStart = _gamePortRangeStart,
                GamePortRangeEnd = _gamePortRangeEnd,
                PublicHost = _publicHost,
                CloudflareApiToken = _cloudflareToken,
                ClearCloudflareApiToken = _clearCloudflareToken,
                CloudflareZoneId = _cloudflareZoneId,
                DnsBaseDomain = _dnsBaseDomain,
                DnsHostLabel = _dnsHostLabel,
                AzureClientId = _azureClientId,
                CurseForgeApiKey = _curseForgeKey,
                ClearCurseForgeApiKey = _clearCurseForgeKey
            };

            var result = await UpdateUseCase.HandleAsync(command, CancellationToken.None);
            if (result.Succeeded)
            {
                Snackbar.Add("Configurações salvas.", Severity.Success);
                await LoadAsync(); // relê: os campos de segredo voltam vazios
            }
            else
                Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }
}
