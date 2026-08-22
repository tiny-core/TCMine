using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Infrastructure.Docker;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Sobe o docker-mailserver como container da instalação.
///     Imagem de terceiro em vez de um MTA escrito aqui: Postfix, OpenDKIM e
///     configuração de TLS são décadas de detalhe acumulado, e errar qualquer um
///     deles não dá erro — dá mensagem entregue na caixa de spam, que ninguém
///     percebe até alguém reclamar.
///     A porta SMTP é publicada SÓ NO LOOPBACK: quem fala com ela é o próprio
///     TCMine, e submissão exposta na internet vira alvo de força bruta no dia
///     seguinte. O envio para fora sai do container pela porta 25, que é
///     iniciada por ele e não precisa de nada publicado.
/// </summary>
public sealed partial class DockerMailServerOrchestrator(
    DockerApiClient docker,
    IOptions<InstanceOptions> options,
    ILogger<DockerMailServerOrchestrator> logger) : IMailServerOrchestrator
{
    private const string ContainerName = "tcmine-mail";
    private const string Image = "mailserver/docker-mailserver:latest";

    private readonly ILogger<DockerMailServerOrchestrator> _logger = logger;

    public async Task<MailServerState> GetStateAsync(CancellationToken ct)
    {
        var container = await FindAsync(ct);

        if (container is null)
            return MailServerState.NotCreated;

        return container switch
        {
            { State: "running" } => MailServerState.Running,
            { State: "restarting" or "created" } => MailServerState.Starting,
            { State: "exited", Status: not null } when container.Status.Contains("(0)") =>
                MailServerState.Stopped,
            { State: "exited" } => MailServerState.Crashed,
            _ => MailServerState.Stopped
        };
    }

    public async Task StartAsync(string domain, CancellationToken ct)
    {
        var existente = await FindAsync(ct);

        if (existente is null)
        {
            await docker.CreateContainerAsync(ContainerName, Spec(domain), ct);
            existente = await FindAsync(ct);
        }

        if (existente is { State: "running" })
            return;

        await docker.StartContainerAsync(existente!.Id, ct);
        Subiu(domain);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (await FindAsync(ct) is { } container)
            await docker.StopContainerAsync(container.Id, 30, ct);
    }

    public async Task RemoveAsync(CancellationToken ct)
    {
        if (await FindAsync(ct) is not { } container)
            return;

        await docker.StopContainerAsync(container.Id, 30, ct);
        await docker.RemoveContainerAsync(container.Id, true, ct);
    }

    public async Task<string?> GetDkimRecordAsync(string domain, CancellationToken ct)
    {
        // A chave é gerada no primeiro arranque e fica num arquivo dentro do
        // container. Ler o arquivo é mais firme que pedir ao setup: o formato do
        // arquivo é estável e o comando muda entre versões da imagem.
        var caminho = $"/tmp/docker-mailserver/opendkim/keys/{domain}/mail.txt";

        var saida = await docker.ExecAsync(ContainerName, ["cat", caminho], ct);

        return string.IsNullOrWhiteSpace(saida) ? null : LimparRegistro(saida);
    }

    public async Task EnsureSenderAccountAsync(string address, string password, CancellationToken ct)
    {
        // O setup do docker-mailserver cria se não existe e troca a senha se
        // existe, então não é preciso perguntar antes qual dos dois é o caso.
        await docker.ExecAsync(ContainerName, ["setup", "email", "add", address, password], ct);
    }

    /// <summary>
    ///     O arquivo do OpenDKIM vem em formato de zona, quebrado em pedaços
    ///     entre aspas e com parênteses. O painel de DNS quer o valor corrido.
    /// </summary>
    private static string LimparRegistro(string zoneFile)
    {
        var pedacos = new List<string>();
        var dentro = false;
        var atual = new System.Text.StringBuilder();

        foreach (var c in zoneFile)
        {
            if (c is '"')
            {
                if (dentro)
                {
                    pedacos.Add(atual.ToString());
                    atual.Clear();
                }

                dentro = !dentro;
                continue;
            }

            if (dentro)
                atual.Append(c);
        }

        return string.Concat(pedacos).Trim();
    }

    private async Task<DockerContainer?> FindAsync(CancellationToken ct)
    {
        var containers = await docker.ListContainersAsync(true, ct);

        return containers.FirstOrDefault(c =>
            c.Names?.Any(n => n.Trim('/') == ContainerName) is true);
    }

    private CreateContainerRequest Spec(string domain)
    {
        var raiz = Path.GetFullPath(Path.Combine(options.Value.RootPath, "mail"));

        Directory.CreateDirectory(Path.Combine(raiz, "data"));
        Directory.CreateDirectory(Path.Combine(raiz, "state"));
        Directory.CreateDirectory(Path.Combine(raiz, "config"));
        Directory.CreateDirectory(Path.Combine(raiz, "logs"));

        return new CreateContainerRequest
        {
            Image = Image,
            Hostname = $"mail.{domain}",
            Env =
            [
                // Só envio: sem antivírus e sem antispam, que existem para o que
                // CHEGA. Ligá-los custaria mais de 1 GB de RAM para filtrar
                // mensagem que este servidor não recebe.
                "ENABLE_CLAMAV=0",
                "ENABLE_SPAMASSASSIN=0",
                "ENABLE_FAIL2BAN=0",

                // DKIM é o que faz a mensagem ser aceita do outro lado.
                "ENABLE_OPENDKIM=1",
                "ENABLE_OPENDMARC=1",

                // Sem certificado próprio a imagem usa um autoassinado. Serve
                // porque quem conecta na submissão é o TCMine, pelo loopback; o
                // que sai para fora usa TLS oportunista do Postfix.
                "SSL_TYPE=",
                "PERMIT_DOCKER=connected-networks"
            ],
            ExposedPorts = new Dictionary<string, object> { ["587/tcp"] = new() },
            Labels = new Dictionary<string, string> { ["tcmine.mail"] = domain },
            HostConfig = new HostConfig
            {
                Binds =
                [
                    $"{Path.Combine(raiz, "data")}:/var/mail",
                    $"{Path.Combine(raiz, "state")}:/var/mail-state",
                    $"{Path.Combine(raiz, "logs")}:/var/log/mail",
                    $"{Path.Combine(raiz, "config")}:/tmp/docker-mailserver"
                ],
                PortBindings = new Dictionary<string, PortBinding[]>
                {
                    // 127.0.0.1 explícito: sem isso o Docker publica em 0.0.0.0.
                    ["587/tcp"] = [new PortBinding { HostIp = "127.0.0.1", HostPort = "587" }]
                },
                RestartPolicy = new RestartPolicy { Name = "unless-stopped" }
            }
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Servidor de e-mail no ar para {Domain}.")]
    private partial void Subiu(string domain);
}
