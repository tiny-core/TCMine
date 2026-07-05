using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TCMine_Server.Infrastructure.FileSystem;

namespace TCMine_Server.Infrastructure.ServerInstances;

/// <summary>
///     Ambiente Docker compartilhado para a orquestração das instâncias (Docker-out-of-Docker): segura o
///     <see cref="DockerClient" /> único (fala com o daemon do host via socket) e resolve a
///     <b>
///         tradução de
///         caminho
///     </b>
///     entre o que o TCMine-Server vê e o que o daemon precisa para os bind-mounts.
///     <para>
///         <b>Tradução de caminho (crítico em DooD):</b> quando o TCMine-Server roda em container, um
///         caminho como <c>/app/tcmine-data/servers/{id}</c> só existe <i>dentro</i> dele; o daemon, no host,
///         precisa do caminho do host (ex.: <c>/media/disco/AppData/tcmine-server/servers/{id}</c>). A config
///         <c>ServerInstances:DataHostRoot</c> aponta para a pasta do host montada em <c>/app/tcmine-data</c>
///         (o caminho do host do próprio <c>tcmine-data</c>, com <b>qualquer nome</b>); sem ela (dev, TCMine fora
///         de container) usamos a pasta <c>tcmine-data</c> local.
///     </para>
///     Singleton: o client é thread-safe e reusado; <see cref="Dispose" /> o fecha no shutdown.
/// </summary>
public sealed class DockerEnvironment : IDisposable
{
    private readonly string? _configuredHostRoot;
    private readonly string _contentRoot;
    private readonly Lock _hostRootLock = new();
    private readonly string _socket;
    private string? _resolvedHostRoot;

    public DockerEnvironment(IConfiguration config, IHostEnvironment env)
    {
        _contentRoot = env.ContentRootPath;

        // Override OPCIONAL do caminho do host da pasta tcmine-data. Se vazio, auto-detectamos inspecionando
        // o próprio container (ver DataHostRoot()) — o admin não precisa repetir um caminho que já está no
        // volume. Só definir esta config se a auto-detecção falhar ou para forçar um caminho.
        _configuredHostRoot = config["ServerInstances:DataHostRoot"]?.Trim() is { Length: > 0 } h ? h : null;

        // Imagem que roda as instâncias. Em produção o compose aponta para a imagem do release (que já
        // traz o JRE) — reuso de imagem, sempre presente no host. Sem config, cai na oficial do temurin
        // (pullável), que funciona em dev sem precisar construir nada.
        McImage = config["ServerInstances:Image"]?.Trim() is { Length: > 0 } img ? img : "eclipse-temurin:25-jre";
        Network = config["ServerInstances:Network"]?.Trim() is { Length: > 0 } net ? net : null;

        _socket = ResolveSocket(config);

        // Timeout infinito no client: o padrão do Docker.DotNet é 100s, mas operações longas como
        // aguardar o instalador do NeoForge (baixa MC + libraries, vários minutos) ou puxar a imagem
        // estouram isso ("The operation has timed out"). O controle de tempo fica com o CancellationToken
        // de cada chamada, não com o HttpClient.
        Client = new DockerClientConfiguration(new Uri(_socket), defaultTimeout: Timeout.InfiniteTimeSpan)
            .CreateClient();
    }

    /// <summary>Client do daemon do host. Thread-safe; reusado por todas as operações.</summary>
    public DockerClient Client { get; }

    /// <summary>Imagem que roda os servidores e os installers efêmeros (só-Java).</summary>
    public string McImage { get; }

    /// <summary>Rede Docker a anexar aos containers (opcional).</summary>
    public string? Network { get; }

    public void Dispose()
    {
        Client.Dispose();
    }

    /// <summary>
    ///     Traduz um caminho local (sob <c>tcmine-data</c>) para o caminho equivalente no host, para uso como
    ///     origem de um bind-mount. Fora de <c>tcmine-data</c>, devolve o caminho como veio. Usa o caminho do
    ///     host de <c>tcmine-data</c> (config explícita ou auto-detectado), então a pasta pode ter qualquer nome.
    /// </summary>
    public string ToHostPath(string localPath)
    {
        var full = Path.GetFullPath(localPath);
        var rel = Path.GetRelativePath(ServerPaths.Data(_contentRoot), full);

        // Caminho fora de tcmine-data (GetRelativePath devolve algo com "..") → sem tradução
        var hostPath = rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel)
            ? full
            : Path.Combine(DataHostRoot(), rel);

        // Normaliza para barra '/': o daemon aceita "P:/dir" (drive Windows com barra normal) e evita
        // a ambiguidade do separador de bind. Em Linux já é '/', então é no-op.
        return hostPath.Replace('\\', '/');
    }

    /// <summary>
    ///     Caminho do host da pasta <c>tcmine-data</c>, resolvido uma vez (cacheado). Ordem: config explícita →
    ///     auto-detecção (inspeciona o próprio container e lê a origem do mount em <c>/app/tcmine-data</c>) →
    ///     pasta local (dev, host == local). Assim, em container com o socket montado, o DooD funciona sem
    ///     precisar da env var — o caminho já vem do próprio volume.
    /// </summary>
    private string DataHostRoot()
    {
        if (_resolvedHostRoot is not null) return _resolvedHostRoot;
        lock (_hostRootLock)
        {
            return _resolvedHostRoot ??=
                _configuredHostRoot ?? TryDetectHostRoot() ?? ServerPaths.Data(_contentRoot);
        }
    }

    /// <summary>
    ///     Inspeciona o próprio container no daemon do host e devolve a origem (host) do mount destinado a
    ///     <c>/app/tcmine-data</c>. Null se não rodar em container ou não achar o mount.
    /// </summary>
    private string? TryDetectHostRoot()
    {
        try
        {
            if (OwnContainerId() is not { } id) return null;

            var info = Client.Containers.InspectContainerAsync(id).GetAwaiter().GetResult();
            var dataDir = ServerPaths.Data(_contentRoot).Replace('\\', '/').TrimEnd('/');

            var mount = info.Mounts?.FirstOrDefault(m =>
                string.Equals(m.Destination?.TrimEnd('/'), dataDir, StringComparison.Ordinal));

            return string.IsNullOrWhiteSpace(mount?.Source) ? null : mount.Source;
        }
        catch
        {
            return null; // sem socket / não é container / daemon indisponível → cai no fallback
        }
    }

    /// <summary>
    ///     Id do próprio container. Lê <c>/proc/self/mountinfo</c> (procura <c>/containers/&lt;id&gt;/</c> dos
    ///     binds que o Docker cria para /etc/hostname etc.) — robusto a cgroup v1/v2 e a data-root customizado.
    ///     Cai no <c>HOSTNAME</c> (id curto, salvo hostname customizado). Null fora de container.
    /// </summary>
    private static string? OwnContainerId()
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/self/mountinfo"))
            {
                var i = line.IndexOf("/containers/", StringComparison.Ordinal);
                if (i < 0) continue;

                var id = new string(line[(i + "/containers/".Length)..].TakeWhile(Uri.IsHexDigit).ToArray());
                if (id.Length >= 12) return id;
            }
        }
        catch
        {
            // /proc indisponível (ex.: Windows dev) → tenta o HOSTNAME
        }

        var hostname = Environment.GetEnvironmentVariable("HOSTNAME");
        return string.IsNullOrWhiteSpace(hostname) ? null : hostname;
    }

    /// <summary>
    ///     Garante que a imagem existe no daemon, baixando-a se faltar. A imagem do release (reusada em
    ///     produção) já está presente — o pull só acontece para imagens públicas em dev (ex.: temurin).
    /// </summary>
    public async Task EnsureImageAsync(string image, CancellationToken ct = default)
    {
        try
        {
            // Já presente? InspectImage lança DockerImageNotFoundException quando não existe localmente
            try
            {
                await Client.Images.InspectImageAsync(image, ct);
                return;
            }
            catch (DockerImageNotFoundException)
            {
                // segue para o pull
            }

            var (repo, tag) = SplitImage(image);
            await Client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = repo, Tag = tag },
                null,
                new Progress<JSONMessage>(),
                ct);
        }
        catch (Exception ex) when (ex is not DockerApiException and not OperationCanceledException)
        {
            // Falha de conexão com o daemon (não é erro de API): socket ausente ou sem permissão.
            // Mensagem acionável — é o primeiro ponto onde o DooD toca o daemon.
            throw new InvalidOperationException(
                $"Não foi possível falar com o daemon Docker em '{_socket}'. No modo Docker-out-of-Docker, " +
                "o container do TCMine-Server precisa: (1) do socket montado " +
                "('/var/run/docker.sock:/var/run/docker.sock' no compose) e (2) de permissão de acesso a ele " +
                "(rode como root ou no grupo docker). Detalhe: " + ex.Message, ex);
        }
    }

    // Separa "repo:tag" em (repo, tag); sem tag explícita assume "latest". O último ':' delimita a tag
    // (cuidado com a porta do registry: só conta como tag se o trecho após ':' não tiver '/').
    private static (string Repo, string Tag) SplitImage(string image)
    {
        var colon = image.LastIndexOf(':');
        if (colon > 0 && !image[(colon + 1)..].Contains('/'))
            return (image[..colon], image[(colon + 1)..]);
        return (image, "latest");
    }

    // Socket do daemon: explícito via config, senão o padrão do SO (npipe no Windows, unix no Linux)
    private static string ResolveSocket(IConfiguration config)
    {
        if (config["ServerInstances:DockerHost"]?.Trim() is { Length: > 0 } host) return host;
        return OperatingSystem.IsWindows() ? "npipe://./pipe/docker_engine" : "unix:///var/run/docker.sock";
    }
}