using System.Net;
using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using TCMine.Server.Web.Background;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Tests.Background;

/// <summary>
///     A caixa de e-mail de teste, exercitada pelo MailKit de verdade.
///     É o único jeito honesto de testar isto: um cliente falso mediria o que eu
///     achei que o protocolo era. Mandando com a mesma biblioteca que o
///     SmtpEmailSender usa, o teste falha se eu tiver entendido SMTP errado.
/// </summary>
public sealed class DevMailServerTests
{
    [Fact]
    public async Task Recebe_o_email_enviado_pelo_cliente_de_verdade()
    {
        var (server, mailbox, port) = await SubirAsync();

        using (server)
        {
            await EnviarAsync(port, "ana@teste.com", "Recuperação de senha", "Abra o link: https://x/y");

            var capturado = mailbox.Recent().ShouldHaveSingleItem();
            capturado.To.ShouldBe("ana@teste.com");
            capturado.Subject.ShouldBe("Recuperação de senha");
            capturado.Body.ShouldContain("https://x/y");
        }
    }

    [Fact]
    public async Task Aceita_cliente_que_autentica()
    {
        // O SmtpEmailSender autentica sempre que há usuário configurado. Se a
        // caixa recusasse AUTH, ela só serviria para quem deixasse o campo
        // vazio — e ninguém descobriria o porquê.
        var (server, mailbox, port) = await SubirAsync();

        using (server)
        {
            await EnviarAsync(port, "ana@teste.com", "Com login", "corpo", usuario: "qualquer");

            mailbox.Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task Desligada_por_padrao_nao_abre_porta()
    {
        // Ligar abre uma porta que aceita qualquer credencial: o padrão precisa
        // ser não abrir.
        var port = PortaLivre();
        var options = new DevMailOptions { Port = port };

        options.Enabled.ShouldBeFalse();

        using var server = new DevMailServer(
            new DevMailbox(), Options.Create(options), NullLogger<DevMailServer>.Instance);

        await server.StartAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<Exception>(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Passar_da_capacidade_descarta_a_mais_antiga()
    {
        var mailbox = new DevMailbox { Capacity = 2 };

        for (var i = 1; i <= 3; i++)
            mailbox.Add(new CapturedEmail(DateTimeOffset.UtcNow, "de@x", "para@y", $"assunto {i}", ""));

        mailbox.Count.ShouldBe(2);

        // Mais recente primeiro, e a primeira mensagem saiu.
        mailbox.Recent().Select(m => m.Subject).ShouldBe(["assunto 3", "assunto 2"]);
    }

    private static async Task<(DevMailServer Server, DevMailbox Mailbox, int Port)> SubirAsync()
    {
        var port = PortaLivre();
        var mailbox = new DevMailbox();

        var server = new DevMailServer(
            mailbox,
            Options.Create(new DevMailOptions { Enabled = true, Port = port }),
            NullLogger<DevMailServer>.Instance);

        await server.StartAsync(TestContext.Current.CancellationToken);

        // O listener sobe noutra tarefa; esperar por ele aceitar conexão é mais
        // firme que dormir um tempo arbitrário.
        await AguardarPortaAsync(port);

        return (server, mailbox, port);
    }

    private static async Task EnviarAsync(
        int port, string para, string assunto, string corpo, string? usuario = null)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("TCMine <nao-responda@teste.com>"));
        message.To.Add(MailboxAddress.Parse(para));
        message.Subject = assunto;
        message.Body = new TextPart("plain") { Text = corpo };

        using var client = new SmtpClient();

        // Sem TLS: a caixa de teste vive no loopback e não anuncia STARTTLS.
        await client.ConnectAsync(
            "127.0.0.1", port, SecureSocketOptions.None, TestContext.Current.CancellationToken);

        if (usuario is not null)
            await client.AuthenticateAsync(usuario, "senha", TestContext.Current.CancellationToken);

        await client.SendAsync(message, TestContext.Current.CancellationToken);
        await client.DisconnectAsync(true, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Porta efêmera pedida ao sistema: fixar um número faria dois testes em
    ///     paralelo (ou uma execução anterior ainda fechando) brigarem por ela.
    /// </summary>
    private static int PortaLivre()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task AguardarPortaAsync(int port)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < limite)
        {
            try
            {
                using var probe = new TcpClient();
                await probe.ConnectAsync(IPAddress.Loopback, port);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(20);
            }
        }
    }
}
