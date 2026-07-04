using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.FileSystem;
using TCMine_Server.Infrastructure.Persistence;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Infrastructure.Launcher;

/// <summary>Estado do job de compilação do launcher.</summary>
public enum LauncherBuildState
{
    Running,
    Succeeded,
    Failed
}

/// <summary>Instantâneo imutável do job de compilação — o que a página renderiza.</summary>
public sealed record LauncherBuildView(
    LauncherBuildState State, string Version, IReadOnlyList<string> Steps, string? Error);

/// <summary>
///     Compila e empacota o launcher <b>pelo próprio servidor</b>, gerando o feed Velopack em
///     <c>tcmine-data/updates</c> (servido em <c>/updates</c>; o <c>Setup.exe</c> em <c>/download</c>).
///
///     <b>Duas versões independentes:</b> o launcher tem a sua própria faixa (<c>launcher-v*</c>),
///     desacoplada do servidor. Para não amarrar as duas, a fonte do launcher <b>não é embutida</b> na
///     imagem — o servidor a <b>baixa do GitHub</b> na tag da versão-alvo (tarball), compila (injetando
///     <c>TcmineServerUrl</c>/<c>MicrosoftClientId</c> das settings) e empacota com o <c>vpk</c>. Assim uma
///     mudança só no launcher (novo <c>launcher-v*</c>) é recompilada pelo servidor <b>em execução</b>, sem
///     rebuild de imagem nem reinício do container; e uma mudança só no servidor não força update do launcher.
///
///     Roda em segundo plano com progresso reconectável (mesmo padrão do <c>ProvisioningCoordinator</c>).
///     Singleton: um build de cada vez. Exige SDK .NET + <c>vpk</c> na imagem (autossuficiente p/ compilar).
/// </summary>
public sealed class LauncherBuildService(
    IServiceScopeFactory scopeFactory,
    ServerSettingsService settings,
    LauncherFeedService feed,
    GitHubReleaseService github,
    IHttpClientFactory http,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<LauncherBuildService> logger)
{
    private readonly object _lock = new();
    private Job? _job;

    /// <summary>Disparado a cada mudança do job — a página re-renderiza.</summary>
    public event Action? Changed;

    /// <summary>Instantâneo do job atual (null = nenhum ainda).</summary>
    public LauncherBuildView? Current
    {
        get
        {
            lock (_lock) return _job?.View();
        }
    }

    /// <summary>Há uma compilação em andamento?</summary>
    public bool IsRunning
    {
        get
        {
            lock (_lock) return _job is { State: LauncherBuildState.Running };
        }
    }

    /// <summary>Ambas as settings embutidas no launcher estão configuradas? (pré-requisito do build).</summary>
    public async Task<bool> SettingsReadyAsync(CancellationToken ct = default)
    {
        var url = await settings.GetPublicBaseUrlAsync(ct);
        var clientId = await settings.GetAzureClientIdAsync(ct);
        return !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(clientId);
    }

    /// <summary>
    ///     Auto-build: se há uma release de launcher (<c>launcher-v*</c>) mais nova que o feed publicado e as
    ///     settings estão prontas, compila essa versão em segundo plano. Idempotente. Devolve se iniciou.
    ///     Usado no boot, ao salvar settings e no poll periódico — o launcher acompanha as releases sem ação
    ///     manual e sem tocar no container.
    /// </summary>
    public async Task<bool> TryStartAutoAsync(CancellationToken ct = default)
    {
        if (IsRunning) return false;

        var tracks = await github.GetAsync(ct: ct);
        var target = tracks.Launcher.LatestVersion;
        var tag = tracks.Launcher.Tag;
        if (target is null || tag is null) return false; // nenhuma release de launcher publicada ainda

        var published = feed.LatestVersion();
        var needs = published is null || AppVersion.IsNewer(target, published);
        if (!needs) return false;

        if (!await SettingsReadyAsync(ct)) return false;

        Start(target, tag, $"Build automático — launcher {target}.");
        return true;
    }

    /// <summary>Inicia (ou ignora, se já rodando) a compilação da <paramref name="tag" /> na
    /// <paramref name="version" />. Retorna já — o trabalho corre em fundo.</summary>
    public void Start(string version, string tag, string notes)
    {
        lock (_lock)
        {
            if (_job is { State: LauncherBuildState.Running }) return;
            _job = new Job(version);
        }

        _ = RunAsync(version, tag, notes);
    }

    private async Task RunAsync(string version, string tag, string notes)
    {
        var job = _job!;
        var stagingRoot = Path.Combine(ServerPaths.Data(env.ContentRootPath), ".launcher-build");

        try
        {
            Report("Verificando configurações…");

            // Embutidas no launcher em build-time (viram AssemblyMetadata). Exigidas antes de compilar.
            var baseUrl = await settings.GetPublicBaseUrlAsync();
            var clientId = await settings.GetAzureClientIdAsync();
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException(
                    "Configure a URL pública (PublicBaseUrl) e o Azure Client Id em Configurações antes de " +
                    "compilar o launcher — ambos são embutidos no launcher em build-time.");

            var vpk = ResolveVpk();
            var rid = config["LauncherBuild:Rid"] is { Length: > 0 } r ? r : "win-x64";
            var channel = config["LauncherBuild:Channel"] is { Length: > 0 } c ? c : "win";

            var updatesDir = ServerPaths.Updates(env.ContentRootPath);
            Directory.CreateDirectory(updatesDir);
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
            Directory.CreateDirectory(stagingRoot);
            var publishDir = Path.Combine(stagingRoot, "publish");
            Directory.CreateDirectory(publishDir);

            // ── 0. Baixa a FONTE do launcher na tag (desacopla do servidor / da imagem) ───────────────
            Report($"Baixando código-fonte do launcher ({tag})…");
            var project = await FetchSourceAsync(tag, Path.Combine(stagingRoot, "src"));

            // ── 1. dotnet publish (self-contained; injeta URL do server + client id do MSAL) ──────────
            Report($"Publicando launcher {version} — {rid}, self-contained…");
            var publishArgs = new List<string>
            {
                "publish", project, "-c", "Release", "-r", rid, "--self-contained", "true",
                "--nologo", "-o", publishDir,
                $"-p:TcmineServerUrl={baseUrl}", $"-p:MicrosoftClientId={clientId}"
            };
            var publish = await RunProcessAsync("dotnet", publishArgs, "Publicando launcher", job);
            if (publish.ExitCode != 0)
                throw new InvalidOperationException(
                    $"'dotnet publish' falhou (exit {publish.ExitCode}).\n{Tail(publish.Output)}");

            // ── 2. vpk pack (gera RELEASES + nupkg + Setup.exe no feed) ───────────────────────────────
            Report("Empacotando com Velopack (vpk pack)…");
            string? notesFile = null;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                notesFile = Path.Combine(stagingRoot, "notes.md");
                await File.WriteAllTextAsync(notesFile, notes);
            }

            // No Linux o `vpk` precisa do DIRETÓRIO de OS `[win]` para cross-compilar o alvo Windows.
            var packArgs = new List<string>();
            if (!OperatingSystem.IsWindows())
                packArgs.Add("[win]");
            packArgs.AddRange(new[]
            {
                "pack", "-u", "TCMine-Launcher", "-v", version, "-p", publishDir,
                "-e", "TCMine-Launcher.exe", "-o", updatesDir, "-c", channel, "-r", rid,
                "--packTitle", "TCMine Launcher"
            });
            if (notesFile is not null)
            {
                packArgs.Add("--releaseNotes");
                packArgs.Add(notesFile);
            }

            var pack = await RunProcessAsync(vpk, packArgs, "Empacotando com Velopack", job);
            if (pack.ExitCode != 0)
                throw new InvalidOperationException(
                    $"'vpk pack' falhou (exit {pack.ExitCode}).\n{Tail(pack.Output)}");

            // ── 3. Registra a release no banco ────────────────────────────────────────────────────────
            Report("Registrando a release…");
            var files = Directory.EnumerateFiles(updatesDir)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .OrderBy(n => n)
                .ToList();

            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Releases.Add(new ReleaseEntity
                {
                    Version = version,
                    Channel = channel,
                    Notes = notes,
                    Files = string.Join('\n', files)
                });
                await db.SaveChangesAsync();
            }

            Report($"Launcher {version} publicado — disponível em /download.");
            job.Complete(null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao compilar o launcher.");
            job.Complete(ex.Message);
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao limpar o staging do build do launcher.");
            }
        }

        Changed?.Invoke();
    }

    // Baixa e extrai o tarball do código-fonte na tag; devolve o caminho do csproj do launcher
    private async Task<string> FetchSourceAsync(string tag, string destDir)
    {
        Directory.CreateDirectory(destDir);
        var url = $"https://github.com/{github.Repo}/archive/refs/tags/{tag}.tar.gz";

        var client = http.CreateClient("github"); // tem User-Agent; segue redirects por padrão
        var tgz = Path.Combine(destDir, "src.tar.gz");
        using (var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Não foi possível baixar a fonte do launcher da tag '{tag}' ({(int)resp.StatusCode}). " +
                    "Confirme que a tag existe no GitHub e o repositório é público (ou defina GITHUB_REPO).");
            await using var fs = File.Create(tgz);
            await resp.Content.CopyToAsync(fs);
        }

        var extractDir = Path.Combine(destDir, "extract");
        Directory.CreateDirectory(extractDir);
        await using (var fs = File.OpenRead(tgz))
        await using (var gz = new GZipStream(fs, CompressionMode.Decompress))
        {
            await TarFile.ExtractToDirectoryAsync(gz, extractDir, overwriteFiles: true);
        }

        // O GitHub empacota tudo sob um único diretório-raiz "{repo}-{ref}"
        var root = Directory.GetDirectories(extractDir).FirstOrDefault()
                   ?? throw new InvalidOperationException("Tarball do código-fonte vazio ou inesperado.");
        var csproj = Path.Combine(root, "TCMine-Launcher", "TCMine-Launcher.csproj");
        if (!File.Exists(csproj))
            throw new InvalidOperationException(
                $"'TCMine-Launcher/TCMine-Launcher.csproj' não encontrado no código-fonte da tag '{tag}'.");
        return csproj;
    }

    // Reporta um marco (novo passo no log) e notifica a UI
    private void Report(string message)
    {
        _job?.Add(message);
        Changed?.Invoke();
    }

    // ── Execução de processo com streaming ─────────────────────────────────────────────────────────

    private async Task<(int ExitCode, string Output)> RunProcessAsync(
        string exe, IReadOnlyList<string> args, string label, Job job)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var output = new StringBuilder();
        var outLock = new object();
        var lastUi = DateTime.MinValue;

        void OnLine(string? line)
        {
            if (string.IsNullOrEmpty(line)) return;
            lock (outLock) output.AppendLine(line);

            var now = DateTime.UtcNow;
            if ((now - lastUi).TotalMilliseconds < 150) return; // throttle
            lastUi = now;
            job.Add($"{label} — {Shorten(line.Trim(), 90)}");
            Changed?.Invoke();
        }

        process.OutputDataReceived += (_, e) => OnLine(e.Data);
        process.ErrorDataReceived += (_, e) => OnLine(e.Data);

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Não foi possível iniciar o processo '{exe}'.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Não foi possível executar '{exe}': {ex.Message}. " +
                "Verifique se está instalado e acessível no PATH (o build exige o SDK .NET e o vpk).");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        string full;
        lock (outLock) full = output.ToString();
        return (process.ExitCode, full);
    }

    private string ResolveVpk()
    {
        var configured = config["LauncherBuild:VpkPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var exe = OperatingSystem.IsWindows() ? "vpk.exe" : "vpk";
        var candidate = Path.Combine(home, ".dotnet", "tools", exe);
        return File.Exists(candidate) ? candidate : "vpk";
    }

    private static string Shorten(string s, int max)
    {
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    private static string Tail(string text, int max = 1500)
    {
        if (string.IsNullOrEmpty(text)) return "(sem saída)";
        return text.Length <= max ? text : "…\n" + text[^max..];
    }

    // ── Estado do job ──────────────────────────────────────────────────────────────────────────────

    private sealed class Job(string version)
    {
        private const int MaxSteps = 80;
        private readonly List<string> _steps = [];
        private readonly object _lock = new();
        private string? _error;

        public string Version { get; } = version;
        public LauncherBuildState State { get; private set; } = LauncherBuildState.Running;

        public void Add(string message)
        {
            lock (_lock)
            {
                static string Label(string s)
                {
                    var i = s.IndexOf(" — ", StringComparison.Ordinal);
                    return i < 0 ? s : s[..i];
                }

                if (_steps.Count > 0 && Label(_steps[^1]) == Label(message))
                    _steps[^1] = message;
                else
                    _steps.Add(message);

                if (_steps.Count > MaxSteps)
                    _steps.RemoveRange(0, _steps.Count - MaxSteps);
            }
        }

        public void Complete(string? error)
        {
            lock (_lock)
            {
                State = error is null ? LauncherBuildState.Succeeded : LauncherBuildState.Failed;
                _error = error;
            }
        }

        public LauncherBuildView View()
        {
            lock (_lock) return new LauncherBuildView(State, Version, [.. _steps], _error);
        }
    }
}
