namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Envio de e-mail transacional (recuperação de senha, convites).
///     A Application não sabe se sai por SMTP, por uma API ou se vai só para o
///     log — isso é escolha de configuração da instalação.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}
