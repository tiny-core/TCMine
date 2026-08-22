using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Security;

/// <summary>
///     Envia e-mail pelo SMTP configurado no painel.
///     MailKit e não o <c>System.Net.Mail.SmtpClient</c> do BCL: a própria
///     Microsoft desaconselha aquele para código novo, e ele não fala SSL
///     implícito na porta 465 — que é o que boa parte dos provedores de e-mail
///     oferece. Trocar de biblioteca depois, com admins já configurados numa
///     porta que não funciona, seria pior.
///     Sem SMTP configurado, delega ao <see cref="LoggingEmailSender" />: a
///     recuperação de senha continua possível (o link vai para o log) em vez de
///     falhar em silêncio numa instalação recém-criada.
/// </summary>
public sealed partial class SmtpEmailSender(
    ISettingsRepository settings,
    LoggingEmailSender fallback,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger = logger;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        // Lido a cada envio, de propósito: a configuração muda pelo painel em
        // runtime, e um valor em cache exigiria invalidação — complexidade que
        // não se paga num envio que acontece uma vez a cada tantos dias.
        var config = await settings.GetAsync(ct);

        // O HasSmtp garante os dois, mas o compilador não sabe disso — e
        // desembrulhar aqui mantém a garantia visível em vez de confiar numa
        // propriedade que alguém pode afrouxar depois.
        if (!config.HasSmtp || config.SmtpHost is not { } host || config.SmtpFrom is not { } from)
        {
            await fallback.SendAsync(to, subject, body, ct);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        // Auto deixa o MailKit escolher entre STARTTLS e SSL implícito pela
        // porta e pelo que o servidor anuncia. A opção do painel é um piso: com
        // TLS desmarcado aceitamos conexão limpa (servidor interno na mesma
        // rede), com ele marcado exigimos criptografia — porque a senha do SMTP
        // viaja nesta conexão.
        var seguranca = config.SmtpUseTls
            ? SecureSocketOptions.Auto
            : SecureSocketOptions.None;

        await client.ConnectAsync(host, config.SmtpPort, seguranca, ct);

        // Servidor interno costuma aceitar retransmissão sem autenticar; só
        // manda credencial quando há uma para mandar.
        if (!string.IsNullOrWhiteSpace(config.SmtpUser))
        {
            // O nome da propriedade engana: o repositório decifra ao ler, então
            // aqui o valor já está em claro. Cifrado é como ele fica GRAVADO.
            await client.AuthenticateAsync(config.SmtpUser, config.SmtpPasswordEncrypted ?? "", ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        Enviado(to, subject);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "E-mail enviado para {Recipient}: {Subject}")]
    private partial void Enviado(string recipient, string subject);
}
