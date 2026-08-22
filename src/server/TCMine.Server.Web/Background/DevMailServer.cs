using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using MimeKit;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Background;

/// <summary>
///     SMTP mínimo que recebe o que o TCMine envia e guarda na
///     <see cref="DevMailbox" />.
///     Escrito à mão em vez de trazer um pacote: o que é preciso falar cabe em
///     meia dúzia de comandos, e uma dependência a mais no projeto inteiro para
///     um recurso de desenvolvimento não se paga. O corpo é entregue ao MimeKit,
///     que já veio junto do MailKit — parsear cabeçalho de e-mail à mão é que
///     seria ingenuidade.
///     ESCUTA SÓ NO LOOPBACK, sempre. Este servidor aceita qualquer credencial e
///     qualquer destinatário; exposto na rede seria um relay aberto, e relay
///     aberto é abusado em horas, não em dias.
/// </summary>
public sealed partial class DevMailServer(
    DevMailbox mailbox,
    IOptions<DevMailOptions> options,
    ILogger<DevMailServer> logger) : BackgroundService
{
    private readonly DevMailOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        var listener = new TcpListener(IPAddress.Loopback, _options.Port);

        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            // Porta ocupada não pode derrubar o painel: isto é acessório.
            NaoSubiu(ex, _options.Port);
            return;
        }

        Escutando(_options.Port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);

                // Sem await: uma conexão lenta não pode impedir a próxima de ser
                // aceita. Cada uma cuida de si e some ao terminar.
                _ = AtenderAsync(client, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Desligando.
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task AtenderAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

                await writer.WriteLineAsync("220 tcmine-devmail");

                string? remetente = null;
                string? destinatario = null;

                while (await reader.ReadLineAsync(ct) is { } linha)
                {
                    var comando = linha.Split(' ', 2)[0].ToUpperInvariant();

                    switch (comando)
                    {
                        case "EHLO":
                            // Anuncia AUTH para o cliente não recusar quando há
                            // usuário configurado: aceitamos qualquer credencial,
                            // porque autenticar numa caixa de teste não protege
                            // nada que já não esteja preso ao loopback.
                            await writer.WriteLineAsync("250-tcmine-devmail");
                            await writer.WriteLineAsync("250-AUTH PLAIN LOGIN");
                            await writer.WriteLineAsync("250 OK");
                            break;

                        case "HELO":
                            await writer.WriteLineAsync("250 tcmine-devmail");
                            break;

                        case "AUTH":
                            await AutenticarAsync(linha, reader, writer, ct);
                            break;

                        case "MAIL":
                            remetente = ExtrairEndereco(linha);
                            await writer.WriteLineAsync("250 OK");
                            break;

                        case "RCPT":
                            destinatario = ExtrairEndereco(linha);
                            await writer.WriteLineAsync("250 OK");
                            break;

                        case "DATA":
                            await writer.WriteLineAsync("354 End data with dot on its own line");
                            await ReceberCorpoAsync(reader, remetente, destinatario, ct);
                            await writer.WriteLineAsync("250 OK");
                            break;

                        case "RSET":
                            remetente = null;
                            destinatario = null;
                            await writer.WriteLineAsync("250 OK");
                            break;

                        case "QUIT":
                            await writer.WriteLineAsync("221 Bye");
                            return;

                        default:
                            // NOOP e o que mais aparecer: responder OK é mais
                            // barato que implementar, e nenhum cliente depende
                            // de uma recusa aqui.
                            await writer.WriteLineAsync("250 OK");
                            break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or FormatException)
            {
                // Conexão cortada no meio ou mensagem malformada: é caixa de
                // teste, e derrubar o serviço por isso seria desproporcional.
                ConexaoFalhou(ex);
            }
        }
    }

    /// <summary>
    ///     Aceita qualquer credencial, consumindo os desafios que o cliente
    ///     espera. AUTH LOGIN é conversado em duas rodadas; AUTH PLAIN costuma
    ///     vir tudo numa linha só.
    /// </summary>
    private static async Task AutenticarAsync(
        string linha, StreamReader reader, StreamWriter writer, CancellationToken ct)
    {
        var partes = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var mecanismo = partes.Length > 1 ? partes[1].ToUpperInvariant() : "";

        if (mecanismo is "LOGIN")
        {
            // Base64 de "Username:" e "Password:", que é o que o protocolo manda
            // enviar como desafio.
            await writer.WriteLineAsync("334 VXNlcm5hbWU6");
            await reader.ReadLineAsync(ct);
            await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
            await reader.ReadLineAsync(ct);
        }
        else if (partes.Length < 3)
        {
            await writer.WriteLineAsync("334 ");
            await reader.ReadLineAsync(ct);
        }

        await writer.WriteLineAsync("235 Authentication successful");
    }

    private async Task ReceberCorpoAsync(
        StreamReader reader, string? remetente, string? destinatario, CancellationToken ct)
    {
        var bruto = new StringBuilder();

        while (await reader.ReadLineAsync(ct) is { } linha)
        {
            if (linha == ".")
                break;

            // Transparência de ponto: o cliente duplica um ponto inicial para
            // ele não ser confundido com o fim da mensagem. Desfazer faz parte
            // do protocolo, não é zelo extra.
            bruto.AppendLine(linha.StartsWith("..", StringComparison.Ordinal) ? linha[1..] : linha);
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(bruto.ToString()));
        var mensagem = await MimeMessage.LoadAsync(stream, ct);

        mailbox.Add(new CapturedEmail(
            DateTimeOffset.UtcNow,
            remetente ?? mensagem.From.ToString(),
            destinatario ?? mensagem.To.ToString(),
            mensagem.Subject ?? "(sem assunto)",
            mensagem.TextBody ?? mensagem.HtmlBody ?? ""));

        Capturada(destinatario ?? "?", mensagem.Subject ?? "");
    }

    /// <summary>Extrai o endereço de MAIL FROM e de RCPT TO, que vêm entre sinais de menor e maior.</summary>
    private static string? ExtrairEndereco(string linha)
    {
        var abre = linha.IndexOf('<');
        var fecha = linha.LastIndexOf('>');

        return abre >= 0 && fecha > abre ? linha[(abre + 1)..fecha] : null;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Caixa de e-mail de teste escutando em 127.0.0.1:{Port}. Aponte o SMTP para ela.")]
    private partial void Escutando(int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Caixa de e-mail de teste não subiu na porta {Port}.")]
    private partial void NaoSubiu(Exception ex, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "E-mail capturado para {Recipient}: {Subject}")]
    private partial void Capturada(string recipient, string subject);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Conexão com a caixa de teste terminou mal.")]
    private partial void ConexaoFalhou(Exception ex);
}
