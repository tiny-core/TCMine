using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCMine.Contracts;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Launcher;

/// <summary>
///     Empacota o launcher embutido na imagem com o <c>vpk</c> e o grava no
///     canal do feed. O endereço desta instalação vai num <c>server.json</c> ao
///     lado do executável — por isso o empacotamento é no ARRANQUE, e não no
///     build: a imagem é uma só para todas as instalações, e só o container
///     sabe o próprio endereço.
/// </summary>
public sealed partial class VelopackLauncherBundle(
    IOptions<LauncherBundleOptions> options,
    ILogger<VelopackLauncherBundle> logger) : ILauncherBundle
{
    private static readonly TimeSpan PackTimeout = TimeSpan.FromMinutes(10);

    private readonly ILogger<VelopackLauncherBundle> _logger = logger;

    public static string Channel => $"win-x64-p{Protocol.Current}";

    public async Task<LauncherBundleOutcome> PublishAsync(Uri? serverUrl, CancellationToken ct)
    {
        var o = options.Value;
        var app = Path.Combine(o.BundlePath, "app");
        var versionFile = Path.Combine(o.BundlePath, "VERSION");

        if (!o.PublishBundled || string.IsNullOrWhiteSpace(o.RootPath)
            || !Directory.Exists(app) || !File.Exists(versionFile))
            return LauncherBundleOutcome.NoBundle;

        var version = (await File.ReadAllTextAsync(versionFile, ct)).Trim();
        var url = serverUrl?.ToString() ?? "";
        var channelDir = VelopackFeed.ChannelDirectory(o.RootPath, Channel);
        Directory.CreateDirectory(channelDir);

        var action = LauncherBundlePlan.Decide(
            version, url, VelopackFeed.LatestVersion(channelDir, Channel), LauncherBundleStamp.Read(channelDir));

        switch (action)
        {
            case LauncherBundleAction.Skip:
                return LauncherBundleOutcome.UpToDate;
            case LauncherBundleAction.KeepNewer:
                return LauncherBundleOutcome.NewerAlreadyPublished;
            case LauncherBundleAction.Rebuild:
                // O vpk recusa a mesma versão duas vezes. Com o endereço mudado,
                // o canal é refeito; quem já tem esta versão não perde nada.
                foreach (var file in Directory.EnumerateFiles(channelDir))
                    File.Delete(file);
                break;
        }

        // O app da imagem é somente leitura e partilhado: o server.json desta
        // instalação vai numa cópia temporária, que é o que o vpk empacota.
        var staging = Path.Combine(Path.GetTempPath(), $"tcmine-launcher-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(app, staging);

            if (serverUrl is not null)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(staging, "server.json"),
                    JsonSerializer.Serialize(new Dictionary<string, string> { ["url"] = url }), ct);
            }

            LogEmpacotando(version, Channel, url);
            await RunVpkAsync(o.BundlePath, version, staging, channelDir, ct);

            new LauncherBundleStamp(version, url).Write(channelDir);
            LogPublicado(version, Channel);
            return LauncherBundleOutcome.Published;
        }
        finally
        {
            try
            {
                Directory.Delete(staging, recursive: true);
            }
            catch (IOException)
            {
                // Temporário: o próximo arranque do container limpa /tmp.
            }
        }
    }

    private static async Task RunVpkAsync(string bundlePath, string version, string packDir, string outputDir, CancellationToken ct)
    {
        // O vpk é uma ferramenta .NET instalada com --tool-path. O executável
        // dela escolhe sozinho o build do runtime certo (o pacote traz um por
        // versão do .NET); só precisa saber onde o .NET está — e é o mesmo que
        // roda este processo.
        var vpk = Path.Combine(bundlePath, "vpk", "vpk");
        if (!File.Exists(vpk))
            throw new InvalidOperationException($"vpk não encontrado em {vpk}.");

        var psi = new ProcessStartInfo(vpk)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var arg in (string[])
                 [
                     "[win]", "-x", "--skip-updates", "pack",
                     "--packId", "TCMine.Launcher",
                     "--packVersion", version,
                     "--packDir", packDir,
                     "--mainExe", "TCMine.Launcher.App.exe",
                     "--packTitle", "TCMine Launcher",
                     "--channel", Channel,
                     "--runtime", "win-x64",
                     "--noPortable",
                     "--outputDir", outputDir
                 ])
            psi.ArgumentList.Add(arg);

        // .../shared/Microsoft.NETCore.App/{versão}/ → três níveis acima é a raiz do .NET.
        psi.Environment["DOTNET_ROOT"] = Path.GetFullPath(
            Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o vpk.");

        // Os dois pipes em PARALELO: drenar um de cada vez enche o buffer do
        // outro e pendura o processo sem erro nenhum (CLAUDE.md §7.1, NeoForge).
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(PackTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        var output = await stdout + await stderr;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"vpk saiu com {process.ExitCode}: {string.Join('\n', output.Split('\n').TakeLast(10))}");
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, dir)));

        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Empacotando o launcher {Version} no canal {Channel} com o endereço '{Url}'.")]
    private partial void LogEmpacotando(string version, string channel, string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Launcher {Version} publicado no canal {Channel}.")]
    private partial void LogPublicado(string version, string channel);
}
