using TCMine.Server.Infrastructure.Docker;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Confere, no arranque, se dá para falar com o Docker.
///     O TCMine existe para orquestrar containers, e mesmo assim subia
///     alegremente sem conseguir abrir o socket — a falha só aparecia quando o
///     admin clicava em "Iniciar", como um aviso na tela que somia com a página.
///     Uma instalação passou dias assim.
///     O motivo é quase sempre o mesmo: o socket pertence ao grupo "docker" com
///     modo 660, e o processo roda como um usuário que não está nesse grupo. Por
///     isso a mensagem diz o que fazer, e não só que falhou — um "não foi
///     possível conectar" mandaria o operador procurar no lugar errado.
///     Não impede a aplicação de subir: o catálogo de modpacks continua útil sem
///     Docker, e derrubar tudo por causa disto seria pior.
/// </summary>
public sealed partial class DockerReachability(
    IServiceScopeFactory scopeFactory,
    ILogger<DockerReachability> logger) : BackgroundService
{
    private readonly ILogger<DockerReachability> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var docker = scope.ServiceProvider.GetRequiredService<DockerApiClient>();

            if (await docker.PingAsync(stoppingToken))
                LogOk();
            else
                LogSemAcesso();
        }
        catch (Exception ex)
        {
            LogFalha(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Docker acessível.")]
    private partial void LogOk();

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Sem acesso ao Docker: nenhum servidor de jogo vai iniciar. "
                  + "O socket costuma ser do grupo 'docker' com modo 660 — confira o GID dele no host "
                  + "(stat -c '%g' /var/run/docker.sock) e acrescente esse número ao group_add do container.")]
    private partial void LogSemAcesso();

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao verificar o acesso ao Docker.")]
    private partial void LogFalha(Exception ex);
}
