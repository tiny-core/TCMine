using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Dns;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Domain.Settings;
using TCMine.Server.Web.Background;
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

    // ---- Modo de rede ----

    /// <summary>
    ///     Os três cartões. Uma lista, e não três blocos de markup: título,
    ///     ícone e explicação mudam juntos, e um cartão a mais é uma linha.
    /// </summary>
    private static readonly NetworkOption[] NetworkOptions =
    [
        new(NetworkMode.Automatic, Icons.Material.Filled.AutoMode, "Automático",
            "O IP desta casa, detectado sozinho. Nada a configurar; o endereço muda quando o IP mudar."),
        new(NetworkMode.OwnHost, Icons.Material.Filled.Language, "Meu domínio ou DDNS",
            "Um nome que você já mantém apontado para esta casa (ex.: um DDNS). O endereço não muda."),
        new(NetworkMode.Cloudflare, Icons.Material.Filled.Cloud, "Domínio na Cloudflare",
            "O TCMine cuida do DNS: cada servidor ganha um nome próprio, sem porta.")
    ];

    private NetworkMode _networkMode = NetworkMode.Automatic;
    private bool _cloudflareUsesPublicHost;

    /// <summary>
    ///     Abre o "Avançado" só se a opção já estava ligada ao carregar. Ligado
    ///     ao valor vivo, desmarcar a caixa fecharia o painel na mão do admin.
    /// </summary>
    private bool _advancedInitiallyOpen;

    private static string CardStyle(bool selected) =>
        selected
            ? "cursor:pointer; height:100%; border:2px solid var(--mud-palette-primary);"
            : "cursor:pointer; height:100%; border:2px solid var(--mud-palette-lines-default);";

    /// <summary>
    ///     A prévia do cartão "O que os jogadores recebem", com o que está
    ///     DIGITADO. Monta uma configuração temporária e passa pela mesma
    ///     <see cref="AddressSettings.From" /> que publica o endereço de verdade.
    /// </summary>
    private IEnumerable<PreviewLine> PreviewLines
    {
        get
        {
            var draft = new InstallationSettings
            {
                NetworkMode = _networkMode,
                CloudflareUsesPublicHost = _cloudflareUsesPublicHost,
                PublicHost = _publicHost,
                LanHost = _lanHost,

                // Só importa SE haverá token, não qual: o valor nunca volta à tela.
                CloudflareApiTokenEncrypted =
                    !_clearCloudflareToken && (_hasCloudflareToken || !string.IsNullOrWhiteSpace(_cloudflareToken))
                        ? "token"
                        : null,
                CloudflareZoneId = _cloudflareZoneId,
                DnsBaseDomain = _dnsBaseDomain,
                DnsHostLabel = _dnsHostLabel
            };

            var address = AddressSettings.From(draft, _detectedAddress);
            var port = _gamePortRangeStart;

            // Modo incompleto não cai no IP na prévia: o Salvar o recusaria, e
            // mostrar o IP faria parecer que está tudo certo.
            var incomplete = _networkMode switch
            {
                NetworkMode.OwnHost when string.IsNullOrWhiteSpace(_publicHost) => "preencha o domínio acima",
                NetworkMode.Cloudflare when !draft.HasCloudflareCredentials => "complete a Cloudflare acima",
                NetworkMode.Cloudflare when _cloudflareUsesPublicHost && string.IsNullOrWhiteSpace(_publicHost) =>
                    "preencha o DDNS ou IP fixo no Avançado",
                _ => null
            };

            var missing = incomplete ?? (_isDetecting ? "detectando o IP…" : "IP público não detectado");

            yield return new PreviewLine(
                $"Servidor na porta {port}", incomplete is null ? address.For(null, port, null) : null, missing);

            if (_networkMode is NetworkMode.Cloudflare)
            {
                yield return new PreviewLine(
                    "Servidor com subdomínio \"sobrevivencia\"",
                    incomplete is null ? address.For(null, port, "sobrevivencia") : null,
                    missing);
            }

            if (address.LanHost is not null)
                yield return new PreviewLine("Quem está em casa", GameAddress.WithPort(address.LanHost, port), "");
        }
    }

    // ---- DNS (Cloudflare) ----
    private bool _clearCloudflareToken;

    /// <summary>Novo token digitado. Vazio = manter o que já está gravado.</summary>
    private string _cloudflareToken = "";

    private string _cloudflareZoneId = "";
    private string _dnsBaseDomain = "";
    private string _dnsHostLabel = "";
    private bool _dnsManualSyncOnly;
    private int _dnsSyncIntervalMinutes = 5;

    /// <summary>
    ///     A última sincronização — a automática, ou a pedida nesta tela. Nulo =
    ///     nenhuma desde que o painel subiu.
    /// </summary>
    private DnsSyncOutcome? _dnsOutcome;

    private DnsSyncReport? _dnsReport => _dnsOutcome?.Report;

    /// <summary>"agora mesmo", "há 4 min": o bastante para saber se o resultado é fresco.</summary>
    private string DnsOutcomeAge
    {
        get
        {
            if (_dnsOutcome is null)
                return "";

            var minutes = (int)(DateTimeOffset.UtcNow - _dnsOutcome.At).TotalMinutes;

            return minutes switch
            {
                < 1 => "agora mesmo",
                < 60 => $"há {minutes} min",
                _ => $"há {minutes / 60} h"
            };
        }
    }

    /// <summary>Só sabemos se existe — o token nunca volta para a tela.</summary>
    private bool _hasCloudflareToken;

    private bool _isSyncingDns;

    private bool _isLoading = true;
    private bool _isSaving;

    /// <summary>Volta preenchido, como o client ID: é público, e o admin precisa conferi-lo.</summary>
    private string _publicHost = "";
    private string _lanHost = "";

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
    [Inject] private DnsSyncState DnsState { get; set; } = default!;

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
            // Fica registrado no mesmo lugar que a automática: a tela mostra
            // sempre a mais recente, venha de onde vier.
            DnsState.Record(await DnsSync.HandleAsync(CancellationToken.None));
            _dnsOutcome = DnsState.Last;
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

        // O modo que VALE, e não o gravado: numa instalação de antes da escolha
        // o gravado é nulo, e o cartão marcado tem de ser o que os jogadores
        // estão recebendo hoje.
        _networkMode = settings.EffectiveNetworkMode;
        _cloudflareUsesPublicHost = settings.EffectiveCloudflareUsesPublicHost;
        _advancedInitiallyOpen = _cloudflareUsesPublicHost;

        _publicHost = settings.PublicHost ?? "";
        _lanHost = settings.LanHost ?? "";

        _hasCloudflareToken = !string.IsNullOrEmpty(settings.CloudflareApiTokenEncrypted);
        _cloudflareZoneId = settings.CloudflareZoneId ?? "";
        _dnsBaseDomain = settings.DnsBaseDomain ?? "";
        _dnsHostLabel = settings.DnsHostLabel ?? "";
        _dnsManualSyncOnly = settings.DnsManualSyncOnly;

        // O que vale, não o gravado: um valor fora da lista no banco aparece
        // como o intervalo que o serviço está de fato usando.
        _dnsSyncIntervalMinutes = (int)settings.DnsSyncInterval.TotalMinutes;
        _cloudflareToken = "";
        _clearCloudflareToken = false;
        _dnsOutcome = DnsState.Last;

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
                NetworkMode = _networkMode,
                CloudflareUsesPublicHost = _networkMode is NetworkMode.Cloudflare && _cloudflareUsesPublicHost,
                PublicHost = _publicHost,
                LanHost = _lanHost,
                CloudflareApiToken = _cloudflareToken,
                ClearCloudflareApiToken = _clearCloudflareToken,
                CloudflareZoneId = _cloudflareZoneId,
                DnsBaseDomain = _dnsBaseDomain,
                DnsHostLabel = _dnsHostLabel,
                DnsManualSyncOnly = _dnsManualSyncOnly,
                DnsSyncIntervalMinutes = _dnsSyncIntervalMinutes,
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

    private sealed record NetworkOption(NetworkMode Mode, string Icon, string Title, string Description);

    /// <param name="Missing">O que dizer quando ainda não há endereço.</param>
    private sealed record PreviewLine(string Label, string? Address, string Missing);
}
