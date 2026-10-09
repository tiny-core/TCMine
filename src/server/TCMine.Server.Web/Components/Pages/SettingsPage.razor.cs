using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Settings;
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

    private bool _hasAzureClientIdFromFile;

    /// <summary>Só sabemos se existe — o valor nunca volta para a tela.</summary>
    private bool _hasCurseForgeKey;

    private bool _isLoading = true;
    private bool _isSaving;
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

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;

        var settings = await Repository.GetAsync(CancellationToken.None);

        _defaultMcVersion = settings.DefaultMinecraftVersion ?? "";
        _defaultLoader = settings.DefaultLoader;
        _defaultMemoryMb = settings.DefaultMemoryMb;
        _worldBackupKeepCount = settings.WorldBackupKeepCount;

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
