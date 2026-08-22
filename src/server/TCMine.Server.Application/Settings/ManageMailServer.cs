using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Settings;

/// <summary>
///     Sobe o servidor de e-mail da instalação e deixa o SMTP já apontado para
///     ele.
///     Preencher as configurações faz parte de subir, e não é conveniência: o
///     admin que tivesse de digitar host, porta e senha depois erraria a senha —
///     ela é gerada aqui e nunca é exibida.
/// </summary>
public sealed partial class StartMailServer(
    IMailServerOrchestrator orchestrator,
    ISettingsRepository settings,
    ICurrentUserScope scope)
{
    /// <summary>
    ///     Conta de envio. Não-resposta de propósito: nada no TCMine lê e-mail,
    ///     e prometer uma caixa que ninguém abre é pior que dizer que não há.
    /// </summary>
    public const string SenderMailbox = "nao-responda";

    public async Task<Result> HandleAsync(string domain, CancellationToken ct)
    {
        // Servidor de e-mail é da instalação inteira, não de um servidor de
        // jogo: quem manda aqui é quem hospeda o TCMine.
        if (!scope.IsInstanceAdmin)
            return Result.Fail("Só o administrador da instalação gerencia o servidor de e-mail.");

        var dominio = domain.Trim().TrimEnd('.').ToLowerInvariant();

        if (!DominioValido(dominio))
            return Result.Fail("Informe um domínio válido, como exemplo.com.");

        var config = await settings.GetAsync(ct);

        try
        {
            await orchestrator.StartAsync(dominio, ct);

            // Senha nova a cada partida do servidor: ela não é digitada por
            // ninguém e não precisa sobreviver: guardá-la cifrada e reescrevê-la
            // é mais simples que sincronizar as duas pontas.
            var senha = RandomNumberGenerator.GetHexString(48);
            var endereco = $"{SenderMailbox}@{dominio}";

            await orchestrator.EnsureSenderAccountAsync(endereco, senha, ct);

            config.MailServerDomain = dominio;
            config.SmtpHost = "127.0.0.1";
            config.SmtpPort = 587;
            config.SmtpUser = endereco;
            config.SmtpPasswordEncrypted = senha;
            config.SmtpFrom = $"TCMine <{endereco}>";

            // Sem TLS porque a conversa é entre dois processos na mesma máquina,
            // pelo loopback. TLS aqui protegeria de um atacante que já está
            // dentro do host — e quem está dentro do host lê a senha no banco.
            config.SmtpUseTls = false;

            await settings.SaveAsync(config, ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            // Docker fora do ar, imagem que não baixa, porta ocupada: a causa é
            // o que o admin precisa ler para agir.
            return Result.Fail($"Não foi possível subir o servidor de e-mail: {ex.Message}");
        }
    }

    /// <summary>
    ///     Validação deliberadamente frouxa: só recusa o que claramente não é
    ///     domínio. Quem decide se ele existe é o DNS, e uma regra apertada aqui
    ///     recusaria um TLD novo que o mundo já aceita.
    /// </summary>
    private static bool DominioValido(string dominio) =>
        dominio.Length is > 3 and <= 253 && DominioPattern().IsMatch(dominio);

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$")]
    private static partial Regex DominioPattern();
}

public sealed class StopMailServer(IMailServerOrchestrator orchestrator, ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(CancellationToken ct)
    {
        if (!scope.IsInstanceAdmin)
            return Result.Fail("Só o administrador da instalação gerencia o servidor de e-mail.");

        try
        {
            await orchestrator.StopAsync(ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Não foi possível parar o servidor de e-mail: {ex.Message}");
        }
    }
}

/// <summary>
///     Estado do servidor e o que falta configurar no DNS.
///     As duas coisas juntas porque a pergunta do admin é uma só: "isto está
///     funcionando?" — e um servidor no ar com DNS sem publicar não está.
/// </summary>
public sealed class GetMailServerView(
    IMailServerOrchestrator orchestrator,
    ISettingsRepository settings)
{
    public async Task<MailServerView> HandleAsync(CancellationToken ct)
    {
        var config = await settings.GetAsync(ct);
        var dominio = config.MailServerDomain;

        MailServerState estado;
        try
        {
            estado = await orchestrator.GetStateAsync(ct);
        }
        catch (Exception)
        {
            // Sem Docker acessível não há servidor: dizer "não criado" é mais
            // útil que propagar a exceção para uma tela de configuração.
            estado = MailServerState.NotCreated;
        }

        if (dominio is null)
            return new MailServerView(estado, null, []);

        var dkim = estado is MailServerState.Running
            ? await SeguroAsync(() => orchestrator.GetDkimRecordAsync(dominio, ct))
            : null;

        return new MailServerView(estado, dominio, MailDnsRecords.For(dominio, dkim, null));
    }

    private static async Task<string?> SeguroAsync(Func<Task<string?>> acao)
    {
        try
        {
            return await acao();
        }
        catch (Exception)
        {
            // A chave ainda pode não existir, ou o container pode estar subindo.
            // A tela mostra os outros registros e volta a pedir depois.
            return null;
        }
    }
}

public sealed record MailServerView(
    MailServerState State,
    string? Domain,
    IReadOnlyList<MailDnsRecord> DnsRecords);
