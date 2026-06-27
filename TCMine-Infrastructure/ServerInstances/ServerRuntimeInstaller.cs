using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCMine_Application.Abstractions;
using TCMine_Domain.Entities;
using TCMine_Domain.Modpack;
using TCMine_Infrastructure.FileSystem;
using TCMine_Infrastructure.Persistence;

namespace TCMine_Infrastructure.ServerInstances;

/// <summary>
/// Garante que a instalação de servidor de uma tupla (loader, versão do loader, versão do Minecraft)
/// existe <b>uma única vez</b> no cache compartilhado (<see cref="ServerPaths.ServerCacheInstalled"/>)
/// e indexa-a em <see cref="ServerRuntimeCacheEntity"/>. É o coração da economia de disco: várias
/// instâncias do mesmo loader/versão reaproveitam o mesmo <c>libraries/</c> pesado em vez de cada uma
/// baixar a sua cópia.
///
/// Faz o download do instalador oficial (NeoForge, via Maven) e roda <c>--installServer</c> através de
/// <see cref="IServerJavaRunner"/> (container Java efêmero) — o próprio instalador baixa o vanilla de
/// que precisa. A execução Java é delegada para manter o provisionamento independente de Docker.
///
/// Hoje cobre <b>NeoForge</b> (o loader padrão dos modpacks); Forge/Fabric/Quilt seguem o mesmo molde
/// (baixar instalador/launcher + rodar) e entram numa etapa seguinte.
/// </summary>
public sealed class ServerRuntimeInstaller(
    AppDbContext db,
    IServerJavaRunner java,
    IHttpClientFactory http,
    IHostEnvironment env)
{
    private readonly string _root = env.ContentRootPath;

    /// <summary>
    /// Devolve a instalação em cache para a tupla pedida, criando-a se ainda não existir. Idempotente:
    /// se já houver linha + pasta, só atualiza <see cref="ServerRuntimeCacheEntity.LastUsedAt"/>.
    /// O caminho absoluto da instalação é dado por <see cref="InstalledDir"/>.
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
            existing.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        // Instala do zero numa pasta limpa (evita lixo de uma tentativa anterior interrompida)
        if (Directory.Exists(installDir)) Directory.Delete(installDir, true);
        Directory.CreateDirectory(installDir);

        await InstallAsync(loader, loaderVersion, installDir, progress, ct);

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
    /// Argumentos passados ao <c>java</c> para iniciar o servidor a partir do diretório da instância
    /// (que terá o <c>libraries/</c> ligado do cache e o <c>user_jvm_args.txt</c> próprio). Derivados
    /// do layout produzido pela instalação, por isso vivem aqui.
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
        ModLoader loader, string loaderVersion, string installDir, IProgress<string>? progress, CancellationToken ct)
    {
        switch (loader)
        {
            case ModLoader.NeoForge:
                await InstallNeoForgeAsync(loaderVersion, installDir, progress, ct);
                break;
            default:
                throw new NotSupportedException(
                    $"Instalação de {ModLoaders.DisplayName(loader)} ainda não implementada " +
                    "(Step 2 cobre NeoForge; os demais entram numa etapa seguinte).");
        }
    }

    // NeoForge: baixa o installer do Maven e roda --installServer (popula libraries/ + args files)
    private async Task InstallNeoForgeAsync(
        string version, string installDir, IProgress<string>? progress, CancellationToken ct)
    {
        // Baixa o installer DENTRO da pasta de instalação: o runner monta só esse diretório como /data,
        // então o jar precisa ser referenciado por nome relativo (não por caminho absoluto do host).
        var installerName = $"neoforge-{version}-installer.jar";
        var installerPath = Path.Combine(installDir, installerName);
        var url = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{version}/{installerName}";

        progress?.Report($"Baixando instalador do NeoForge {version}…");
        await DownloadAsync(url, installerPath, ct);

        // O installer baixa o vanilla server.jar e as libraries; nós só geramos user_jvm_args.txt depois.
        progress?.Report($"Instalando NeoForge {version} (baixa o Minecraft e as libraries — pode demorar na 1ª vez)…");
        var result = await java.RunAsync(installDir, ["-jar", installerName, "--installServer"], ct);
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

    private async Task DownloadAsync(string url, string destPath, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        var client = http.CreateClient();
        await using var net = await client.GetStreamAsync(url, ct);
        await using var fs = File.Create(destPath);
        await net.CopyToAsync(fs, ct);
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
