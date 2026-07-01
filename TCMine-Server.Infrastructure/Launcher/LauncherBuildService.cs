using System.Diagnostics;
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
///     <c>tcmine-data/updates</c> (servido em <c>/updates</c>; o <c>Setup.exe</c> vai em <c>/download</c>).
///     Fluxo: <c>dotnet publish</c> do launcher (injetando <c>TcmineServerUrl</c> = PublicBaseUrl e
///     <c>MicrosoftClientId</c> = AzureClientId) → <c>vpk pack</c> (Velopack) → grava um
///     <see cref="ReleaseEntity" />.
///
///     Roda em <b>segundo plano com progresso reconectável</b> (mesmo padrão do
///     <c>ProvisioningCoordinator</c>): a página se inscreve em <see cref="Changed" /> e sobrevive a um
///     refresh. Singleton: só há um launcher, logo um job de cada vez.
///
///     Pré-requisitos de ambiente: o servidor precisa rodar a partir do <b>código-fonte</b> (para achar o
///     projeto do launcher), com o <b>SDK .NET</b> e o <b>vpk</b> disponíveis. Sem isso, falha com
///     mensagem clara — não é suportado num deploy runtime-only.
/// </summary>
public sealed class LauncherBuildService(
    IServiceScopeFactory scopeFactory,
    ServerSettingsService settings,
    LauncherFeedService feed,
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

    /// <summary>
    ///     Versão-alvo do launcher = a versão CORRENTE do servidor (modelo de uma versão só). O launcher é
    ///     sempre compilado para casar com o servidor que está rodando.
    /// </summary>
    public string TargetVersion => AppVersion.Current(config);

    /// <summary>O feed publicado está atrás da versão do servidor (ou não existe)? → precisa (re)compilar.</summary>
    public bool NeedsBuild()
    {
        var published = feed.LatestVersion();
        return published is null || AppVersion.IsNewer(TargetVersion, published);
    }

    /// <summary>Ambas as settings embutidas no launcher estão configuradas? (pré-requisito do build).</summary>
    public async Task<bool> SettingsReadyAsync(CancellationToken ct = default)
    {
        var url = await settings.GetPublicBaseUrlAsync(ct);
        var clientId = await settings.GetAzureClientIdAsync(ct);
        return !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(clientId);
    }

    /// <summary>
    ///     Auto-build: compila o launcher na <see cref="TargetVersion" /> se estiver desatualizado e as
    ///     settings prontas. Idempotente (não reinicia se já rodando). Devolve se iniciou. Usado no boot e
    ///     ao salvar as settings — o launcher acompanha o servidor sem ação manual.
    /// </summary>
    public async Task<bool> TryStartAutoAsync(CancellationToken ct = default)
    {
        if (IsRunning || !NeedsBuild()) return false;
        if (!await SettingsReadyAsync(ct)) return false;

        Start(TargetVersion, $"Build automático — launcher alinhado ao servidor {TargetVersion}.");
        return true;
    }

    /// <summary>Inicia (ou ignora, se já rodando) a compilação. Retorna já — o trabalho corre em fundo.</summary>
    public void Start(string version, string notes)
    {
        lock (_lock)
        {
            if (_job is { State: LauncherBuildState.Running }) return;
            _job = new Job(version);
        }

        _ = RunAsync(version, notes);
    }

    private async Task RunAsync(string version, string notes)
    {
        var job = _job!;
        var stagingRoot = Path.Combine(ServerPaths.Data(env.ContentRootPath), ".launcher-build");

        try
        {
            Report("Verificando ambiente e configurações…");

            // Ambas são EMBUTIDAS no launcher em build-time (viram AssemblyMetadata) — sem elas o launcher
            // não saberia o endereço do servidor nem faria o login Microsoft. Exigidas antes de compilar.
            var baseUrl = await settings.GetPublicBaseUrlAsync();
            var clientId = await settings.GetAzureClientIdAsync();
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException(
                    "Configure a URL pública (PublicBaseUrl) e o Azure Client Id em Configurações antes de " +
                    "compilar o launcher — ambos são embutidos no launcher em build-time.");
            var project = ResolveLauncherProject();
            var vpk = ResolveVpk();
            var rid = config["LauncherBuild:Rid"] is { Length: > 0 } r ? r : "win-x64";
            var channel = config["LauncherBuild:Channel"] is { Length: > 0 } c ? c : "win";

            var updatesDir = ServerPaths.Updates(env.ContentRootPath);
            Directory.CreateDirectory(updatesDir);
            var publishDir = Path.Combine(stagingRoot, "publish");
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
            Directory.CreateDirectory(publishDir);

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

            // No Linux, o `vpk` precisa do DIRETÓRIO de OS `[win]` para cross-compilar o alvo Windows
            // (gera o Setup.exe/nupkg de Windows). No Windows nativo o pack já mira Windows — sem diretório.
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
                    Notes = notes ?? string.Empty,
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
                logger.LogWarning(ex, "Falha ao limpar o diretório de staging do build do launcher.");
            }
        }

        Changed?.Invoke();
    }

    // Reporta um marco (novo passo no log) e notifica a UI
    private void Report(string message)
    {
        _job?.Add(message);
        Changed?.Invoke();
    }

    // ── Execução de processo com streaming ─────────────────────────────────────────────────────────

    // Roda um processo capturando stdout+stderr; reflete a última linha (coalescida, com throttle de
    // 150ms) sob o passo <paramref name="label"/> e acumula a saída completa para o diagnóstico de falha.
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
            if ((now - lastUi).TotalMilliseconds < 150) return; // throttle: não martela o circuito
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
                "Verifique se está instalado e acessível no PATH (o build do launcher exige o SDK .NET e o vpk).");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        string full;
        lock (outLock) full = output.ToString();
        return (process.ExitCode, full);
    }

    // ── Resolução de caminhos ──────────────────────────────────────────────────────────────────────

    private string ResolveLauncherProject()
    {
        var configured = config["LauncherBuild:ProjectPath"];
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return Path.GetFullPath(configured);

        // Dev: o content root é .../TCMine-Server; o launcher é um projeto-irmão na solução
        var root = env.ContentRootPath;
        string[] candidates =
        [
            Path.Combine(root, "..", "TCMine-Launcher", "TCMine-Launcher.csproj"),
            Path.Combine(root, "TCMine-Launcher", "TCMine-Launcher.csproj")
        ];
        foreach (var c in candidates)
            if (File.Exists(c))
                return Path.GetFullPath(c);

        throw new InvalidOperationException(
            "Código-fonte do launcher não encontrado. Compilar o launcher só é possível quando o servidor " +
            "roda a partir do código-fonte da solução (defina LauncherBuild:ProjectPath se o layout for outro).");
    }

    private string ResolveVpk()
    {
        var configured = config["LauncherBuild:VpkPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        // Caminho padrão da ferramenta global do dotnet; se não existir, deixa o PATH resolver "vpk"
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var exe = OperatingSystem.IsWindows() ? "vpk.exe" : "vpk";
        var candidate = Path.Combine(home, ".dotnet", "tools", exe);
        return File.Exists(candidate) ? candidate : "vpk";
    }

    // ── Helpers de texto ───────────────────────────────────────────────────────────────────────────

    private static string Shorten(string s, int max)
    {
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    // Últimas linhas da saída (para a mensagem de erro não ficar gigante)
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
                // Coalesce atualizações ao vivo do mesmo passo (mesmo rótulo antes de " — ")
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
