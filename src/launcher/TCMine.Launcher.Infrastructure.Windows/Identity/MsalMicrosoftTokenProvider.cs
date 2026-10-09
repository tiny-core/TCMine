using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Identity;
// O MSAL também declara um LogLevel, e ele não tem nada que ver com o nosso: é o
// do logger interno da biblioteca. Sem o alias, cada [LoggerMessage] abaixo fica
// ambíguo e o gerador não produz método nenhum.
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace TCMine.Launcher.Infrastructure.Windows.Identity;

/// <summary>
///     O primeiro salto da autenticação: provar a conta Microsoft do jogador.
///     É a única parte não portável da cadeia, e por isso a única que vive aqui.
///     O que vem depois — Xbox Live, XSTS, Minecraft Services — é HTTPS comum e
///     está na infraestrutura portável, atrás da porta que esta classe implementa.
///     O cache é do MSAL, deliberadamente. Guardar o refresh token à mão parece
///     mais simples e não fecha: um public client não tem API para reinjetar um
///     refresh token vindo de fora, então o login silencioso passa
///     obrigatoriamente pelo cache dele. No Windows o Extensions.Msal persiste-o
///     com DPAPI, que é exatamente o que faríamos — e no dia do Linux ele já sabe
///     usar o keyring.
/// </summary>
public sealed partial class MsalMicrosoftTokenProvider(
    string cacheDirectory,
    IParentWindowHandle window,
    ILogger<MsalMicrosoftTokenProvider> logger) : IMicrosoftTokenProvider, IDisposable
{
    /// <summary>
    ///     O que o Minecraft exige. O <c>offline_access</c> é o que faz a
    ///     Microsoft emitir refresh token: sem ele o cache guarda um access token
    ///     de uma hora e o jogador volta a fazer login interativo a cada sessão.
    /// </summary>
    private static readonly string[] Scopes = ["XboxLive.signin", "offline_access"];

    private readonly ILogger<MsalMicrosoftTokenProvider> _logger = logger;

    /// <summary>
    ///     Serializa a construção da app. Ela é cara (lê e decifra o cache do
    ///     disco) e não pode acontecer duas vezes em paralelo: dois helpers
    ///     registados sobre o mesmo ficheiro disputam-no.
    /// </summary>
    private readonly SemaphoreSlim _porta = new(1, 1);

    private IPublicClientApplication? _app;
    private string? _clientIdDaApp;

    /// <summary>
    ///     Só o semáforo. A app do MSAL não é descartável e o cache persistido
    ///     não tem nada aberto entre chamadas — o contentor descarta este
    ///     singleton no fecho da aplicação, e é o suficiente.
    /// </summary>
    public void Dispose() => _porta.Dispose();

    public async Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct)
    {
        try
        {
            var app = await GetAppAsync(azureClientId, ct);

            // Nenhuma conta em cache é o primeiro arranque de toda instalação —
            // resultado normal, e a tela trata-o em silêncio.
            if ((await app.GetAccountsAsync()).FirstOrDefault() is not { } account)
                return AuthResult.NoStoredCredentials();

            var result = await app.AcquireTokenSilent(Scopes, account).ExecuteAsync(ct);

            return AuthResult.Success(result.AccessToken);
        }
        catch (MsalUiRequiredException)
        {
            // Havia conta, mas o refresh token expirou ou foi revogado. Do ponto
            // de vista do arranque é o mesmo que não haver nada: o jogador
            // precisa de entrar, e dizer-lhe "a sua credencial expirou" antes de
            // ele ter pedido alguma coisa é ruído.
            LogNeedsInteraction();
            return AuthResult.NoStoredCredentials();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (MsalException ex)
        {
            LogFailed(ex, ex.ErrorCode);
            return MicrosoftSignInFailures.Traduzir(ex.ErrorCode, ex.Message);
        }
        // Qualquer exceção que não seja do MSAL (ex.: falha ao resolver o HWND
        // do broker) não pode escapar até o BlazorWebView: sem um handler de
        // UnhandledException ligado, o clique morreria em silêncio — o jogador
        // via o botão "não reagir" e nada mais. Aqui é silencioso por design (é
        // a tentativa automática do arranque), mas registrado.
        catch (Exception ex)
        {
            LogFailed(ex, ex.GetType().Name);
            return AuthResult.NoStoredCredentials();
        }
    }

    public async Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct)
    {
        try
        {
            var app = await GetAppAsync(azureClientId, ct);

            // Com broker, o diálogo é o do Windows e o jogador que já usa a
            // conta Microsoft na máquina entra sem escrever nada. O pai importa:
            // sem ele o diálogo abre ATRÁS do launcher e parece que travou.
            // Sem broker disponível, o MSAL cai para o navegador do SISTEMA —
            // nunca uma WebView embutida. É a diferença entre ver a barra de
            // endereço da Microsoft e ser convidado a escrever a palavra-passe
            // numa janela que qualquer um podia ter desenhado.
            //
            // window.Handle, NUNCA () => window.Handle: esta versão do MSAL
            // (4.89.0) não tem sobrecarga WithParentActivityOrWindow(Func<IntPtr>)
            // — só (object) e (IntPtr). Um lambda cai na de object, que guarda o
            // delegate como valor opaco e NUNCA o invoca; o broker então nunca lê
            // handle nenhum e recusa com "window_handle_required" mesmo com a
            // janela na tela. Ler a propriedade aqui, na hora, é seguro: SignInAsync
            // só roda a partir de um clique, com a janela garantidamente visível.
            var result = await app.AcquireTokenInteractive(Scopes)
                .WithParentActivityOrWindow(window.Handle)
                .WithUseEmbeddedWebView(false)
                .ExecuteAsync(ct);

            return AuthResult.Success(result.AccessToken);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (MsalException ex)
        {
            LogFailed(ex, ex.ErrorCode);
            return MicrosoftSignInFailures.Traduzir(ex.ErrorCode, ex.Message);
        }
        // Aqui a tentativa "pode falar" (veio de um clique): em vez de deixar
        // escapar e o clique parecer morto, vira uma falha visível na tela de
        // login — mesmo tratamento que um MsalException já recebia.
        catch (Exception ex)
        {
            LogFailed(ex, ex.GetType().Name);
            return AuthResult.Failed($"Falha inesperada ao entrar: {ex.Message}");
        }
    }

    public async Task SignOutAsync(CancellationToken ct)
    {
        // Sem client id não há app construída, e não haver app significa que
        // ninguém entrou nesta execução — não há o que remover.
        if (_app is not { } app)
            return;

        foreach (var account in await app.GetAccountsAsync())
            await app.RemoveAsync(account);

        LogSignedOut();
    }

    /// <summary>
    ///     Constrói a app uma vez e reaproveita-a.
    ///     O client id vem do handshake, e não de uma constante, porque cada
    ///     servidor autentica contra a app dele — reconstruir a cada chamada
    ///     perderia o cache em memória e faria cada ecrã reabrir o ficheiro.
    ///     Trocar de servidor troca o client id e obriga a reconstruir; é raro, e
    ///     por isso não há dicionário aqui.
    /// </summary>
    private async Task<IPublicClientApplication> GetAppAsync(string azureClientId, CancellationToken ct)
    {
        if (_app is { } pronta && _clientIdDaApp == azureClientId)
            return pronta;

        await _porta.WaitAsync(ct);

        try
        {
            if (_app is { } jaConstruida && _clientIdDaApp == azureClientId)
                return jaConstruida;

            var app = PublicClientApplicationBuilder.Create(azureClientId)

                // Contas pessoais, e só. Conta de Minecraft é sempre conta
                // pessoal; aceitar o audience organizacional deixaria o jogador
                // entrar com a conta do trabalho e falhar no Xbox Live, longe daqui.
                .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.PersonalMicrosoftAccount)

                // O broker do Windows (WAM). Quando não está disponível — versão
                // antiga, política da máquina —, o MSAL cai sozinho para o
                // navegador; por isso o redirect de loopback continua aqui, e por
                // isso a tela de configurações manda registar os DOIS URIs.
                .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
                .WithRedirectUri("http://localhost")
                .WithClientName("TCMine Launcher")
                .Build();

            await RegisterCacheAsync(app, azureClientId);

            _app = app;
            _clientIdDaApp = azureClientId;

            return app;
        }
        finally
        {
            _porta.Release();
        }
    }

    /// <summary>
    ///     Liga o cache persistente. Um ficheiro POR client id: dois servidores
    ///     partilharem o mesmo ficheiro faria sair de um encerrar a sessão no
    ///     outro, e as contas até podem ser diferentes.
    ///     Falhar aqui não impede entrar — só impede lembrar. É por isso que a
    ///     exceção é engolida: ficar sem armazenamento seguro (perfil móvel,
    ///     política de grupo) degrada o login para "entrar a cada arranque", que é
    ///     incómodo; recusar o arranque por causa disso seria pior.
    /// </summary>
    private async Task RegisterCacheAsync(IPublicClientApplication app, string azureClientId)
    {
        try
        {
            var propriedades = new StorageCreationPropertiesBuilder(
                    $"msal-{azureClientId}.cache", cacheDirectory)
                .Build();

            var helper = await MsalCacheHelper.CreateAsync(propriedades);

            helper.RegisterCache(app.UserTokenCache);
        }
        catch (MsalCachePersistenceException ex)
        {
            LogNoSecureStorage(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Credencial da Microsoft precisa de interação; tratada como ausente.")]
    private partial void LogNeedsInteraction();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login com a Microsoft falhou ({Code}).")]
    private partial void LogFailed(Exception ex, string? code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Contas da Microsoft removidas desta máquina.")]
    private partial void LogSignedOut();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Sem armazenamento seguro para o cache do MSAL: o jogador terá de entrar a cada arranque.")]
    private partial void LogNoSecureStorage(Exception ex);
}
