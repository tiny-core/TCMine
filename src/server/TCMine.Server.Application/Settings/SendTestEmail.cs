using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Settings;

/// <summary>
///     Manda uma mensagem de teste para o endereço que o admin indicar.
///     Existe porque a alternativa é descobrir que o SMTP está errado no dia em
///     que alguém esquece a senha — quando a pessoa que precisa do e-mail é
///     justamente a que não consegue entrar para consertar a configuração.
/// </summary>
public sealed class SendTestEmail(IEmailSender email, ISettingsRepository settings)
{
    public async Task<Result> HandleAsync(string to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(to))
            return Result.Fail("Informe o endereço de destino.");

        var config = await settings.GetAsync(ct);

        // Sem SMTP o envio "funciona" (cai no log) e o admin veria sucesso sem
        // ter recebido nada. Aqui a resposta precisa ser sobre a configuração,
        // não sobre a entrega.
        if (!config.HasSmtp)
            return Result.Fail("Configure o servidor e o remetente de SMTP antes de testar.");

        try
        {
            await email.SendAsync(
                to.Trim(),
                "Teste de e-mail — TCMine",
                """
                Se você está lendo isto, o envio de e-mail do TCMine está funcionando.

                É por este caminho que saem os links de recuperação de senha.
                """,
                ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            // A falha do SMTP é a informação útil do teste: autenticação
            // recusada, host errado, TLS incompatível. Engolir a mensagem
            // original deixaria o admin adivinhando.
            return Result.Fail($"Não foi possível enviar: {ex.Message}");
        }
    }
}
