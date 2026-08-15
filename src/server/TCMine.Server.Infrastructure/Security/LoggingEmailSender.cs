using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Security;

/// <summary>
///     Enviador de e-mail de reserva: escreve a mensagem no log em vez de mandar.
///     Serve a instalação que ainda não configurou SMTP — a recuperação de senha
///     continua funcionando (o admin pega o link no log do servidor) em vez de
///     falhar em silêncio. Quando houver SMTP configurado, ele assume o lugar.
/// </summary>
public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger = logger;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        LogEmail(to, subject, body);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "SMTP não configurado — e-mail não enviado. Para: {Recipient} | Assunto: {Subject}\n{Body}")]
    private partial void LogEmail(string recipient, string subject, string body);
}
