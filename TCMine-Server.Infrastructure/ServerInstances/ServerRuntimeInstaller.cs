using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCMine_Application.Abstractions;
using TCMine_Domain.Entities;
using TCMine_Domain.Modpack;
using TCMine_Server.Infrastructure.FileSystem;
using TCMine_Server.Infrastructure.Persistence;

namespace TCMine_Server.Infrastructure.ServerInstances;

/// <summary>
///     Garante que a instalação de servidor de uma tupla (loader, versão do loader, versão do Minecraft)
///     existe <b>uma única vez</b> no cache compartilhado (<see cref="ServerPaths.ServerCacheInstalled" />)
///     e indexa-a em <see cref="ServerRuntimeCacheEntity" />. É o coração da economia de disco: várias
///     instâncias do mesmo loader/versão reaproveitam o mesmo <c>libraries/</c> pesado em vez de cada uma
///     baixar a sua cópia.
///     Faz o download do instalador oficial (NeoForge, via Maven) e roda <c>--installServer</c> através de
///     <see cref="IServerJavaRunner" /> (container Java efêmero) — o próprio instalador baixa o vanilla de
///     que precisa. A execução Java é delegada para manter o provisionamento independente de Docker.
///     Hoje cobre <b>NeoForge</b> (o loader padrão dos modpacks); Forge/Fabric/Quilt seguem o mesmo molde
///     (baixar instalador/launcher + rodar) e entram numa etapa seguinte.
/// </summary>
public sealed class ServerRuntimeInstaller(
    AppDbContext db,
    IServerJavaRunner java,
    IHttpClientFactory http,
    IHostEnvironment env)
{
    private readonly string _root = env.ContentRootPath;

    /// <summary>
    ///     Devolve a instalação em cache para a tupla pedida, criando-a se ainda não existir. Idempotente:
    ///     se já houver linha + pasta, só atualiza <see cref="ServerRuntimeCacheEntity.LastUsedAt" />.
    ///     O caminho absoluto da instalação é dado por <see cref="InstalledDir" />.
    /// </summary>
    public async Task<ServerRuntimeCacheEntity> EnsureAsync(
        ModLoader loader, string loaderVersion, string minecraftVersion,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var existing = await db.ServerRuntimeCache.FirstOrDefaultAsync(
            c => c.Loader == loader && c.LoaderVersion == loaderVersion && c.MinecraftVersion == minecraftVersion, ct);

        var slug = Slug(loader, loaderVersion, minecraftVersion);
        var installDir = Path.Combine(ServerPaths.ServerCacheInstalled(_root), slug);

        // Cache válido = linha no banco + instalação REAL no disco. Validamos pelo artefato (libraries/),
        // não só pela pasta: uma tentativa anterior quebrada pode ter deixado a pasta vazia + a linha no
        // banco — nesse caso reinstala (auto-cura), em vez de reaproveitar um install inválido.
        if (existing is not null && Directory.Exists(Path.Combine(installDir, "libraries")))
        {
            progress?.Report(
                $"Runtime {ModLoaders.DisplayName(loader)} {loaderVersion} (MC {minecraftVersion}) já em cache — reutilizando.");
            existing.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        // Instala do zero numa pasta limpa (evita lixo de uma tentativa anterior interrompida)
        progress?.Report(
            $"Runtime {ModLoaders.DisplayName(loader)} {loaderVersion} (MC {minecraftVersion}) não está em cache — instalando…");
        if (Directory.Exists(installDir)) Directory.Delete(installDir, true);
        Directory.CreateDirectory(installDir);

        // Nome fixo do container do instalador, por slug → handle estável para retomar/limpar após queda
        await InstallAsync(loader, loaderVersion, installDir, $"tcmine-install-{slug}", progress, ct);

        var size = DirectorySize(installDir);

        // Upsert do índice: cria a linha, ou atualiza a existente cuja pasta tinha sumido
        if (existing is null)
        {
            existing = new ServerRuntimeCacheEntity
            {
                Loader = loader, LoaderVersion = loaderVersion, MinecraftVersion = minecraftVersion,
                RelativePath = slug, SizeBytes = size
            };
            db.ServerRuntimeCache.Add(existing);
        }
        else
        {
            existing.RelativePath = slug;
            existing.SizeBytes = size;
        }

        existing.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return existing;
    }

    /// <summary>Caminho absoluto da instalação em cache a partir do <c>RelativePath</c> indexado.</summary>
    public string InstalledDir(ServerRuntimeCacheEntity cache)
    {
        return Path.Combine(ServerPaths.ServerCacheInstalled(_root), cache.RelativePath);
    }

    /// <summary>
    ///     Argumentos passados ao <c>java</c> para iniciar o servidor a partir do diretório da instância
    ///     (que terá o <c>libraries/</c> ligado do cache e o <c>user_jvm_args.txt</c> próprio). Derivados
    ///     do layout produzido pela instalação, por isso vivem aqui.
    /// </summary>
    public static IReadOnlyList<string> ResolveLaunchArgs(ModLoader loader, string loaderVersion)
    {
        return loader switch
        {
            // NeoForge gera libraries/net/neoforged/neoforge/<ver>/unix_args.txt (sempre Linux no container)
            ModLoader.NeoForge =>
            [
                "@user_jvm_args.txt",
                $"@libraries/net/neoforged/neoforge/{loaderVersion}/unix_args.txt",
                "nogui"
            ],
            _ => throw new NotSupportedException(
                $"Launch de {ModLoaders.DisplayName(loader)} ainda não implementado (Step 2 cobre NeoForge).")
        };
    }

    // ── Instalação por loader ───────────────────────────────────────────────────────────────────────

    private async Task InstallAsync(
        ModLoader loader, string loaderVersion, string installDir, string containerName,
        IProgress<string>? progress, CancellationToken ct)
    {
        switch (loader)
        {
            case ModLoader.NeoForge:
                await InstallNeoForgeAsync(loaderVersion, installDir, containerName, progress, ct);
                break;
            default:
                throw new NotSupportedException(
                    $"Instalação de {ModLoaders.DisplayName(loader)} ainda não implementada " +
                    "(Step 2 cobre NeoForge; os demais entram numa etapa seguinte).");
        }
    }

    // NeoForge: baixa o installer do Maven e roda --installServer (popula libraries/ + args files)
    private async Task InstallNeoForgeAsync(
        string version, string installDir, string containerName, IProgress<string>? progress, CancellationToken ct)
    {
        // Baixa o installer DENTRO da pasta de instalação: o runner monta só esse diretório como /data,
        // então o jar precisa ser referenciado por nome relativo (não por caminho absoluto do host).
        var installerName = $"neoforge-{version}-installer.jar";
        var installerPath = Path.Combine(installDir, installerName);
        var url = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{version}/{installerName}";

        await DownloadAsync(url, installerPath, $"Baixando instalador do NeoForge {version}", progress, ct);

        var label = $"Instalando NeoForge {version}";

        // Espelha a saída do instalador com um HEARTBEAT: um timer publica a cada 2s a última linha + o
        // tempo decorrido (mm:ss). O instalador tem fases longas e SILENCIOSAS (ex.: o RENAME/ART depois de
        // "Splitting … files", que processa milhares de classes sem imprimir); sem o heartbeat o overlay
        // congelaria na última linha e "pareceria travado". A saída só atualiza a última linha; o timer é
        // quem publica na UI (também evita martelar o circuito com as centenas de linhas do instalador).
        var started = DateTime.UtcNow;
        var lineLock = new object();
        var lastLine = "baixando Minecraft e libraries (pode demorar na 1ª vez)…";

        void PublishStatus()
        {
            if (progress is null) return;
            string detail;
            lock (lineLock)
            {
                detail = lastLine;
            }

            progress.Report($"{label} — {Shorten(detail, 60)} · {DateTime.UtcNow - started:mm\\:ss}");
        }

        var installOutput = new Progress<string>(line =>
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) return;
            lock (lineLock)
            {
                lastLine = trimmed;
            }
        });

        // Timer de 2s: publica já e depois periodicamente (mm:ss sobe mesmo nas fases sem saída)
        using var heartbeat = progress is null
            ? null
            : new Timer(_ => PublishStatus(), null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

        var result = await java.RunAsync(installDir, ["-jar", installerName, "--installServer"],
            progress is null ? null : installOutput, containerName, ct);
        if (!result.Success)
            throw new InvalidOperationException(
                $"Instalador do NeoForge {version} falhou (exit {result.ExitCode}).\n{result.Output}");

        // Sanidade pós-install: o container saiu com sucesso, mas o libraries/ tem que ter aparecido NO
        // diretório que o TCMine-Server enxerga. Se não apareceu, o container escreveu noutro lugar (bind
        // apontando para outro caminho) — comum no Docker-out-of-Docker em Windows. Inclui o que o
        // instalador imprimiu e o que de fato caiu na pasta, para diagnóstico imediato.
        if (!Directory.Exists(Path.Combine(installDir, "libraries")))
        {
            var landed = Directory.Exists(installDir)
                ? string.Join(", ", Directory.GetFileSystemEntries(installDir).Select(Path.GetFileName))
                : "(pasta não existe)";

            throw new InvalidOperationException(
                "O instalador do NeoForge terminou (exit 0), mas 'libraries/' não apareceu em " +
                $"'{installDir}'. Isso quase sempre é o bind-mount apontando para outro lugar (o container " +
                "do installer escreveu num caminho do host diferente do que o TCMine-Server lê). " +
                $"\n\nConteúdo que apareceu na pasta: [{landed}]" +
                $"\n\nSaída do instalador:\n{Truncate(result.Output, 2000)}");
        }

        // Instalador já cumpriu o papel; só libraries/ importa daqui pra frente
        File.Delete(installerPath);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────────

    // Baixa reportando o progresso em bytes (rótulo fixo + detalhe variável após " — ", para o log
    // coalescer as atualizações numa linha só). Throttle de ~250ms para não inundar o circuito Blazor.
    private async Task DownloadAsync(
        string url, string destPath, string label, IProgress<string>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        var client = http.CreateClient();

        using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? 0;

        progress?.Report($"{label} — iniciando…");

        await using var net = await resp.Content.ReadAsStreamAsync(ct);
        await using var fs = File.Create(destPath);

        var buffer = new byte[81920];
        long read = 0;
        var lastReport = DateTime.MinValue;
        int n;
        while ((n = await net.ReadAsync(buffer, ct)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;

            var now = DateTime.UtcNow;
            if (progress is not null && (now - lastReport).TotalMilliseconds >= 250)
            {
                lastReport = now;
                progress.Report(total > 0
                    ? $"{label} — {FormatSize(read)} / {FormatSize(total)} ({read * 100 / total}%)"
                    : $"{label} — {FormatSize(read)}");
            }
        }

        // Linha final "completa" (garante 100% mesmo que o último tick tenha sido engolido pelo throttle)
        progress?.Report(total > 0
            ? $"{label} — {FormatSize(total)} concluído (100%)"
            : $"{label} — {FormatSize(read)} concluído");
    }

    // Formata bytes em KB/MB/GB para as mensagens de progresso
    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 KB";
        var kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0} KB";
        var mb = kb / 1024.0;
        return mb >= 1024 ? $"{mb / 1024:0.#} GB" : $"{mb:0.#} MB";
    }

    // Encurta uma linha de saída do instalador para caber na linha de progresso do overlay
    private static string Shorten(string s, int max)
    {
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    // Corta um texto longo (saída do instalador) para a mensagem de erro não ficar gigante
    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "(sem saída)";
        return text.Length <= max ? text : text[..max] + "\n… (truncado)";
    }

    private static long DirectorySize(string dir)
    {
        return new DirectoryInfo(dir)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }

    // Slug de pasta para a tupla (ex.: "neoforge-21.1.77-mc1.21.1")
    private static string Slug(ModLoader loader, string loaderVersion, string mc)
    {
        return $"{ModLoaders.DisplayName(loader).ToLowerInvariant()}-{loaderVersion}-mc{mc}";
    }
}