using TCMine.Server.Application.Security;

namespace TCMine.Server.Web.Background;

/// <summary>
///     No arranque, se nenhum administrador consegue entrar, emite o código de
///     resgate e o escreve no log. O log é o canal certo justamente por ser o
///     que só quem opera o servidor lê (<c>docker compose logs</c>): a página de
///     login é pública, e "quem entrar primeiro vira admin" entregaria a
///     instalação a qualquer conta Microsoft que passasse por ali.
/// </summary>
public sealed partial class AdminClaimBootstrap(
    IServiceScopeFactory scopeFactory,
    AdminClaimCode codes,
    ILogger<AdminClaimBootstrap> logger) : BackgroundService
{
    private readonly ILogger<AdminClaimBootstrap> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var claim = scope.ServiceProvider.GetRequiredService<ClaimInstanceAdmin>();

            if (await claim.IsNeededAsync(stoppingToken))
                LogCodigo(codes.Issue());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalha(ex);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Nenhum administrador consegue entrar (contas antigas sem Microsoft). Entre com a sua conta "
                  + "Microsoft, abra /admin/claim e informe o código {Code}. Ele vale uma vez, até o próximo arranque.")]
    private partial void LogCodigo(string code);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao verificar se a instalação tem um administrador.")]
    private partial void LogFalha(Exception ex);
}
