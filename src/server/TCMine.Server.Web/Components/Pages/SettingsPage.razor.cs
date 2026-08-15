using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Settings;

namespace TCMine.Server.Web.Components.Pages;

public partial class SettingsPage : ComponentBase
{
    private bool _clearCurseForgeKey;
    private bool _clearSmtpPassword;

    // Novos valores digitados. Vazio = manter o que já está gravado.
    private string _curseForgeKey = "";
    private ModLoader _defaultLoader = ModLoader.NeoForge;
    private string _defaultMcVersion = "";
    private int _defaultMemoryMb = 4096;
    private int _worldBackupKeepCount = 5;

    /// <summary>Só sabemos se existe — o valor nunca volta para a tela.</summary>
    private bool _hasCurseForgeKey;

    private bool _hasSmtpPassword;
    private bool _isLoading = true;
    private bool _isSaving;
    private string _smtpFrom = "";
    private string _smtpHost = "";
    private string _smtpPassword = "";
    private int _smtpPort = 587;
    private bool _smtpUseTls = true;
    private string _smtpUser = "";

    [Inject] private ISettingsRepository Repository { get; set; } = default!;
    [Inject] private UpdateSettings UpdateUseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override async Task OnInitializedAsync() => await LoadAsync();

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
        _hasSmtpPassword = !string.IsNullOrEmpty(settings.SmtpPasswordEncrypted);

        _smtpHost = settings.SmtpHost ?? "";
        _smtpPort = settings.SmtpPort;
        _smtpUser = settings.SmtpUser ?? "";
        _smtpFrom = settings.SmtpFrom ?? "";
        _smtpUseTls = settings.SmtpUseTls;

        _curseForgeKey = "";
        _smtpPassword = "";
        _clearCurseForgeKey = false;
        _clearSmtpPassword = false;

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
                CurseForgeApiKey = _curseForgeKey,
                ClearCurseForgeApiKey = _clearCurseForgeKey,
                SmtpHost = _smtpHost,
                SmtpPort = _smtpPort,
                SmtpUser = _smtpUser,
                SmtpPassword = _smtpPassword,
                ClearSmtpPassword = _clearSmtpPassword,
                SmtpFrom = _smtpFrom,
                SmtpUseTls = _smtpUseTls
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
