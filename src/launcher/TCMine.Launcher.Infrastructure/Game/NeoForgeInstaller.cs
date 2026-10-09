using System.Diagnostics;
using CmlLib.Core;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Game;

/// <summary>
///     Instala o NeoForge, que é o único loader sem instalador no CmlLib.
///     E é justamente o padrão dos packs modernos, então não havia como o deixar
///     de fora. A saída é a mesma dos outros: uma entrada em <c>versions/</c> que
///     o CmlLib abre como qualquer outra.
///     O caminho é o oficial: o instalador do NeoForge é um programa Java, e nós
///     já temos um JRE gerido (ver <c>IJavaLocator</c>). Corremo-lo em modo
///     silencioso contra a raiz compartilhada. Reimplementar o que ele faz —
///     aplicar patches ao jar do cliente, gerar o JSON da versão — seria refazer
///     um programa que muda a cada release do NeoForge.
/// </summary>
internal sealed class NeoForgeInstaller(HttpClient http, ILogger logger)
{
    private static readonly Action<ILogger, int, string, string, Exception?> LogInstallerFailed =
        LoggerMessage.Define<int, string, string>(
            LogLevel.Error,
            new EventId(1, nameof(LogInstallerFailed)),
            "Instalador do NeoForge saiu com {Codigo}. stderr: {Erro} stdout: {Saida}");

    public async Task<string> InstallAsync(
        string minecraftVersion,
        string neoForgeVersion,
        string javaPath,
        MinecraftPath layout,
        IProgress<GameLaunchProgress>? progress,
        CancellationToken ct)
    {
        var nomeDaVersao = $"neoforge-{neoForgeVersion}";

        // Já instalado: o JSON da versão existir é o sinal, e é o mesmo que o
        // CmlLib procura para abrir. Reinstalar custaria minutos a cada clique.
        if (File.Exists(Path.Combine(layout.Versions, nomeDaVersao, $"{nomeDaVersao}.json")))
            return nomeDaVersao;

        // A raiz do .minecraft para o instalador é onde estão versions/ e
        // libraries/ — a COMPARTILHADA, não a da instância. É lá que ele escreve,
        // e é de lá que o CmlLib lê.
        var raiz = Directory.GetParent(layout.Versions)!.FullName;

        Directory.CreateDirectory(raiz);
        EnsureLauncherProfile(raiz);

        var jar = await DownloadInstallerAsync(neoForgeVersion, ct);

        try
        {
            progress?.Report(new GameLaunchProgress($"Instalando NeoForge {neoForgeVersion}"));

            await RunInstallerAsync(javaPath, jar, raiz, ct);
        }
        finally
        {
            File.Delete(jar);
        }

        if (!File.Exists(Path.Combine(layout.Versions, nomeDaVersao, $"{nomeDaVersao}.json")))
        {
            throw new InvalidOperationException(
                $"O instalador do NeoForge {neoForgeVersion} terminou mas não deixou a versão "
                + $"{nomeDaVersao}. Confirme que a versão existe para o Minecraft {minecraftVersion}.");
        }

        return nomeDaVersao;
    }

    private async Task<string> DownloadInstallerAsync(string neoForgeVersion, CancellationToken ct)
    {
        var url = "https://maven.neoforged.net/releases/net/neoforged/neoforge/"
                  + $"{neoForgeVersion}/neoforge-{neoForgeVersion}-installer.jar";

        var target = Path.Combine(Path.GetTempPath(), $"neoforge-{Guid.CreateVersion7():N}.jar");

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            // 404 aqui quer dizer versão que não existe, e essa é a informação
            // útil: o admin publicou um pack com um NeoForge que ninguém tem.
            throw new InvalidOperationException(
                $"O NeoForge {neoForgeVersion} não está disponível no repositório oficial "
                + $"(HTTP {(int)response.StatusCode}).");
        }

        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var file = File.Create(target))
            await source.CopyToAsync(file, ct);

        return target;
    }

    private async Task RunInstallerAsync(
        string javaPath,
        string jar,
        string raiz,
        CancellationToken ct)
    {
        var info = new ProcessStartInfo(javaPath)
        {
            // --install-client em modo headless. Sem isto o instalador abre uma
            // janela Swing e fica à espera de alguém clicar, dentro de um
            // launcher que não tem como mostrá-la.
            ArgumentList = { "-jar", jar, "--install-client", raiz },
            WorkingDirectory = raiz,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(info)
                            ?? throw new InvalidOperationException(
                                "Não foi possível executar o instalador do NeoForge.");

        // Lidas em paralelo: o instalador escreve bastante, e não drenar os dois
        // canais enche o buffer do pipe e trava o processo para sempre — sem
        // erro, sem saída, só um jogo que nunca abre.
        var saida = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        if (process.ExitCode is 0)
            return;

        LogInstallerFailed(logger, process.ExitCode, (await error).Trim(), (await saida).Trim(), null);

        throw new InvalidOperationException(
            $"O instalador do NeoForge terminou com erro {process.ExitCode}.");
    }

    /// <summary>
    ///     O instalador recusa-se a correr sem um <c>launcher_profiles.json</c>.
    ///     É herança do tempo em que só existia o launcher oficial: ele quer
    ///     escrever um perfil lá. Um ficheiro com um objeto vazio satisfaz a
    ///     verificação, e o perfil que ele grava é ignorado por nós.
    /// </summary>
    private static void EnsureLauncherProfile(string raiz)
    {
        var path = Path.Combine(raiz, "launcher_profiles.json");

        if (!File.Exists(path))
            File.WriteAllText(path, """{"profiles":{},"version":3}""");
    }
}
