using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.QuiltMC;
using CmlLib.Core.ProcessBuilder;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Game;

/// <summary>
///     Abre o jogo, com o CmlLib a fazer o trabalho sujo.
///     É o único lugar do produto onde não escrevemos o motor, e por decisão: o
///     índice da Mojang, as bibliotecas, os assets, os nativos e o classpath são
///     plumbing igual em todo launcher — invisível quando certo, e detectável só
///     na máquina do jogador quando errado. Não é onde o TCMine se diferencia.
///     A dependência é aceitável porque está atrás do <see cref="IGameLauncher" />:
///     trocar de motor é reescrever esta classe, e nada acima dela sabe que o
///     CmlLib existe.
/// </summary>
public sealed partial class CmlLibGameLauncher(
    HttpClient http,
    LauncherPaths paths,
    ILogger<CmlLibGameLauncher> logger) : IGameLauncher
{
    private readonly ILogger<CmlLibGameLauncher> _logger = logger;

    public async Task<GameLaunchResult> LaunchAsync(
        GameLaunchRequest request,
        IProgress<GameLaunchProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            var layout = BuildLayout(request.InstanceDirectory);
            var launcher = new MinecraftLauncher(layout);

            progress?.Report(new GameLaunchProgress("Preparando o Minecraft"));

            var version = await ResolveVersionAsync(launcher, layout, request, progress, ct);

            var options = new MLaunchOption
            {
                Path = layout,
                JavaPath = request.JavaPath,

                Session = BuildSession(request),

                MaximumRamMb = request.MemoryMb ?? 4096
            };

            var process = await launcher.InstallAndBuildProcessAsync(
                version,
                options,
                new Progress<CmlLib.Core.Installers.InstallerProgressChangedEventArgs>(
                    e => progress?.Report(new GameLaunchProgress($"Baixando: {e.Name}"))),
                new Progress<CmlLib.Core.ByteProgress>(
                    b => progress?.Report(new GameLaunchProgress("Baixando arquivos do jogo", b.ToRatio()))),
                ct);

            // Sem janela de consola e sem shell, e com os dois canais redirigidos:
            // é por isso que o locator escolheu java.exe e não javaw.exe. Tem de
            // ser ANTES do Start — depois, o redirecionamento é ignorado e o log
            // fica vazio sem dizer porquê.
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            progress?.Report(new GameLaunchProgress("Abrindo o jogo"));

            process.Start();

            LogStarted(version, process.Id);

            return GameLaunchResult.Ok(new SystemGameProcess(process));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A causa real vale mais do que uma frase nossa: é ela que diz se
            // adianta tentar de novo, e o jogador vai colá-la a pedir ajuda.
            LogFailed(ex, request.MinecraftVersion, request.Loader.ToString());

            return GameLaunchResult.Failed($"Não foi possível abrir o jogo. {ex.Message}");
        }
    }

    /// <summary>
    ///     A sessão que o jogo apresenta à Mojang.
    ///     Sem token é o modo offline: parte-se da sessão offline do CmlLib, que
    ///     traz as convenções dele para os campos que não conhecemos, e o UUID é
    ///     substituído pelo que guardámos. Isso importa mais do que parece — o
    ///     mundo do jogador é indexado pelo UUID, e deixar o CmlLib inventar um a
    ///     partir do nome faria a partida offline abrir com inventário e posição
    ///     de outra pessoa.
    /// </summary>
    private static MSession BuildSession(GameLaunchRequest request)
    {
        if (request.AccessToken is not { Length: > 0 } token)
        {
            var offline = MSession.CreateOfflineSession(request.PlayerName);
            offline.UUID = request.PlayerUuid;

            return offline;
        }

        return new MSession
        {
            Username = request.PlayerName,
            AccessToken = token,
            UUID = request.PlayerUuid
        };
    }

    /// <summary>
    ///     Pasta da instância para o jogo, pastas COMPARTILHADAS para o resto.
    ///     O <c>BasePath</c> é o que vira o diretório de trabalho do jogo — é lá
    ///     que ficam <c>mods/</c>, <c>config/</c> e os mundos, que são por
    ///     instância. Bibliotecas, versões e assets apontam para uma raiz comum,
    ///     porque são idênticos entre packs: dez modpacks em 1.21 partilham
    ///     centenas de megabytes em vez de os baixarem dez vezes — a mesma lógica
    ///     do content store para os mods.
    /// </summary>
    private MinecraftPath BuildLayout(string instanceDirectory)
    {
        var shared = Path.Combine(paths.RootDirectory, "minecraft");

        return new MinecraftPath(instanceDirectory)
        {
            Library = Path.Combine(shared, "libraries"),
            Versions = Path.Combine(shared, "versions"),
            Assets = Path.Combine(shared, "assets"),
            Runtime = Path.Combine(shared, "runtime")
        };
    }

    /// <summary>
    ///     Instala o loader, se preciso, e devolve o nome da versão a abrir.
    ///     Cada loader instala-se de um jeito e todos acabam no mesmo sítio: uma
    ///     entrada em <c>versions/</c> que o CmlLib sabe abrir. O NeoForge é o
    ///     único que não tem instalador na biblioteca — ver
    ///     <see cref="NeoForgeInstaller" />.
    /// </summary>
    private async Task<string> ResolveVersionAsync(
        MinecraftLauncher launcher,
        MinecraftPath layout,
        GameLaunchRequest request,
        IProgress<GameLaunchProgress>? progress,
        CancellationToken ct)
    {
        var mc = request.MinecraftVersion;
        var loader = request.LoaderVersion;

        if (request.Loader is not ModLoader.Vanilla)
            progress?.Report(new GameLaunchProgress($"Instalando {request.Loader} {loader}"));

        return request.Loader switch
        {
            ModLoader.Vanilla => mc,

            ModLoader.Fabric => await new FabricInstaller(http).Install(mc, Require(loader, "Fabric"), layout),

            ModLoader.Quilt => await new QuiltInstaller(http).Install(mc, Require(loader, "Quilt"), layout),

            ModLoader.Forge => await new ForgeInstaller(launcher).Install(
                mc,
                Require(loader, "Forge"),
                new ForgeInstallOptions
                {
                    JavaPath = request.JavaPath,

                    // Reinstalar a cada abertura custaria minutos e baixaria o
                    // mesmo outra vez; o instalador já sabe reconhecer o que fez.
                    SkipIfAlreadyInstalled = true,
                    CancellationToken = ct
                }),

            ModLoader.NeoForge => await new NeoForgeInstaller(http, _logger).InstallAsync(
                mc, Require(loader, "NeoForge"), request.JavaPath, layout, progress, ct),

            _ => throw new NotSupportedException($"Loader não suportado: {request.Loader}.")
        };
    }

    /// <summary>
    ///     Um pack com loader mas sem versão de loader não é instalável, e o erro
    ///     tem de dizer isso — sem esta verificação o CmlLib receberia nulo e
    ///     falharia com uma mensagem sobre argumentos.
    /// </summary>
    private static string Require(string? loaderVersion, string loader) =>
        string.IsNullOrWhiteSpace(loaderVersion)
            ? throw new InvalidOperationException(
                $"Este modpack usa {loader} mas não diz qual versão do loader. Avise o administrador.")
            : loaderVersion;

    [LoggerMessage(Level = LogLevel.Information, Message = "Jogo aberto: versão {Version}, processo {Pid}.")]
    private partial void LogStarted(string version, int pid);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao abrir o jogo ({Minecraft}, {Loader}).")]
    private partial void LogFailed(Exception ex, string minecraft, string loader);
}
