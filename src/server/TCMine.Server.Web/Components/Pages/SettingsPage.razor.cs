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

    private bool _hasAzureClientIdFromFile;

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
    private string _testEmailTo = "";
    private bool _testing;

    private MailServerView? _mail;
    private string _mailDomain = "";
    private bool _mailBusy;

    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISettingsRepository Repository { get; set; } = default!;
    [Inject] private IOptions<ServerOptions> ServerOptions { get; set; } = default!;
    [Inject] private UpdateSettings UpdateUseCase { get; set; } = default!;
    [Inject] private SendTestEmail TestEmailUseCase { get; set; } = default!;
    [Inject] private StartMailServer StartMailUseCase { get; set; } = default!;
    [Inject] private StopMailServer StopMailUseCase { get; set; } = default!;
    [Inject] private GetMailServerView MailViewUseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await CarregarMailAsync();
    }

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

        _azureClientId = settings.AzureClientId ?? "";
        _azureClientIdFromFile = ServerOptions.Value.AzureClientId;
        _hasAzureClientIdFromFile = !string.IsNullOrWhiteSpace(_azureClientIdFromFile);

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
                AzureClientId = _azureClientId,
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

    private async Task SendTestAsync()
    {
        if (_testing)
            return;

        _testing = true;

        try
        {
            var result = await TestEmailUseCase.HandleAsync(_testEmailTo, CancellationToken.None);

            if (result.Succeeded)
            {
                Snackbar.Add(
                    $"Mensagem enviada para {_testEmailTo}. Confira a caixa de entrada e o spam.",
                    Severity.Success);
            }
            else
            {
                // Erro do SMTP é a informação útil do teste, então vai inteiro
                // para a tela em vez de virar "falhou".
                Snackbar.Add(result.Error!, Severity.Error);
            }
        }
        finally
        {
            _testing = false;
        }
    }

    private Color EstadoCor => _mail?.State switch
    {
        MailServerState.Running => Color.Success,
        MailServerState.Starting => Color.Info,
        MailServerState.Crashed => Color.Error,
        MailServerState.Stopped => Color.Warning,
        _ => Color.Default
    };

    private string EstadoTexto => _mail?.State switch
    {
        MailServerState.Running => "no ar",
        MailServerState.Starting => "subindo",
        MailServerState.Crashed => "caiu",
        MailServerState.Stopped => "parado",
        _ => "não criado"
    };

    private async Task CarregarMailAsync()
    {
        _mail = await MailViewUseCase.HandleAsync(CancellationToken.None);

        // Só preenche o campo quando ele está vazio: sobrescrever o que o admin
        // acabou de digitar seria perder o trabalho dele a cada recarga.
        if (_mailDomain.Length is 0 && _mail.Domain is { } dominio)
            _mailDomain = dominio;
    }

    private Task SubirServidorAsync() => ComMailOcupado(async () =>
    {
        var result = await StartMailUseCase.HandleAsync(_mailDomain, CancellationToken.None);

        if (result.Succeeded)
        {
            Snackbar.Add(
                "Servidor no ar e SMTP apontado para ele. Publique os registros de DNS abaixo.",
                Severity.Success);

            // Recarrega a configuração: o caso de uso reescreveu host, porta,
            // usuário e remetente, e a tela ainda mostra o que havia antes.
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    });

    private Task PararServidorAsync() => ComMailOcupado(async () =>
    {
        var result = await StopMailUseCase.HandleAsync(CancellationToken.None);

        if (!result.Succeeded)
            Snackbar.Add(result.Error!, Severity.Error);
    });

    private async Task ComMailOcupado(Func<Task> acao)
    {
        if (_mailBusy)
            return;

        _mailBusy = true;

        try
        {
            await acao();
            await CarregarMailAsync();
        }
        finally
        {
            _mailBusy = false;
        }
    }
}
