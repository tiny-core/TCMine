using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Runtime;

/// <summary>
///     Baixa e guarda os JREs que o jogo precisa, do Adoptium (Temurin).
///     Nós gerimos o Java, e não o sistema: deixar o autodetect escolher acha um
///     Java 8 esquecido no PATH e transforma um modpack moderno num crash sem
///     explicação. Um JRE por major, partilhado por todas as instâncias que o
///     exigem — três packs em 1.21 baixam um Java, não três.
///     Portável de propósito. Só a escolha de sistema/arquitectura e do extrator
///     muda entre plataformas, e ambas saem do <see cref="RuntimeInformation" />:
///     pôr isto no projeto do Windows faria o port reescrever um download e um
///     unzip.
/// </summary>
public sealed partial class AdoptiumJavaLocator(
    HttpClient http,
    LauncherPaths paths,
    ILogger<AdoptiumJavaLocator> logger) : IJavaLocator
{
    private readonly ILogger<AdoptiumJavaLocator> _logger = logger;

    public async Task<string> EnsureRuntimeAsync(
        int majorVersion,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var destino = Path.Combine(
            paths.RuntimesDirectory, majorVersion.ToString(CultureInfo.InvariantCulture));

        // Caminho quente: já está no disco. Acontece em todo arranque depois do
        // primeiro, e não pode custar uma ida à rede — o jogador clicou em jogar.
        if (Localizar(destino) is { } jaInstalado)
            return jaInstalado;

        var pacote = await DescobrirAsync(majorVersion, ct);

        LogBaixando(majorVersion, pacote.Name!);

        var arquivo = await BaixarAsync(pacote, progress, ct);

        try
        {
            ExtrairParaDestino(arquivo, pacote.Name!, destino);
        }
        finally
        {
            // O arquivo comprimido não serve para mais nada e são ~45 MB.
            File.Delete(arquivo);
        }

        return Localizar(destino)
               ?? throw new InvalidOperationException(
                   $"O JRE {majorVersion} foi extraído mas não tem executável em bin/. "
                   + "O formato do pacote do Adoptium mudou.");
    }

    public Task<IReadOnlyList<InstalledRuntime>> ListAsync(CancellationToken ct)
    {
        if (!Directory.Exists(paths.RuntimesDirectory))
            return Task.FromResult<IReadOnlyList<InstalledRuntime>>([]);

        var runtimes = new List<InstalledRuntime>();

        foreach (var directory in Directory.EnumerateDirectories(paths.RuntimesDirectory))
        {
            // Só pastas cujo nome é um major. Um ".tmp" de extração interrompida
            // não é um JRE, e listá-lo ofereceria ao jogador apagar algo que o
            // download seguinte vai limpar sozinho.
            if (int.TryParse(Path.GetFileName(directory), CultureInfo.InvariantCulture, out var major))
                runtimes.Add(new InstalledRuntime(major, TamanhoDe(directory)));
        }

        return Task.FromResult<IReadOnlyList<InstalledRuntime>>(runtimes);
    }

    public Task RemoveAsync(int majorVersion, CancellationToken ct)
    {
        var directory = Path.Combine(
            paths.RuntimesDirectory, majorVersion.ToString(CultureInfo.InvariantCulture));

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
            LogRemovido(majorVersion);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Quanto ocupa uma pasta, somando tudo.
    ///     Ficheiro inacessível conta zero em vez de atirar: isto serve um número
    ///     na interface, e falhar a contagem inteira por causa de um ficheiro
    ///     bloqueado seria trocar uma estimativa por nada.
    /// </summary>
    private static long TamanhoDe(string directory)
    {
        try
        {
            return new DirectoryInfo(directory)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>
    ///     O executável dentro de um JRE já extraído, ou nulo se não houver.
    ///     Procura em <c>*/bin/</c> e não recursivamente: o arquivo do Adoptium
    ///     tem uma pasta raiz com o nome da build (<c>jdk-21.0.5+11-jre</c>), que
    ///     muda a cada release, e varrer uma árvore de milhares de ficheiros a
    ///     cada clique em jogar seria pagar caro por um caminho previsível.
    /// </summary>
    private static string? Localizar(string destino)
    {
        if (!Directory.Exists(destino))
            return null;

        // java.exe e NÃO javaw.exe, apesar de o javaw ser o costume em
        // launchers. O javaw é do subsistema gráfico e não escreve em stdout:
        // com ele, mostrar o log do jogo na interface deixa de ser possível, e
        // um crash vira "fechou sozinho". O java.exe com CreateNoWindow não
        // mostra consola nenhuma e mantém a saída capturável.
        var executavel = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";

        return Directory.EnumerateDirectories(destino)
            .Select(pasta => Path.Combine(pasta, "bin", executavel))
            .FirstOrDefault(File.Exists);
    }

    private async Task<AdoptiumPackage> DescobrirAsync(int majorVersion, CancellationToken ct)
    {
        var url = "https://api.adoptium.net/v3/assets/latest/"
                  + $"{majorVersion}/hotspot?image_type=jre&vendor=eclipse"
                  + $"&os={SistemaOperativo()}&architecture={Arquitectura()}";

        var assets = await http.GetFromJsonAsync(url, AdoptiumJsonContext.Default.AdoptiumAssetArray, ct);

        var pacote = assets?.FirstOrDefault()?.Binary?.Package;

        // Sem link ou sem checksum não há download seguro possível. Falhar aqui,
        // com a combinação pedida no texto, poupa investigar um erro de extração
        // mais à frente que não teria nada a ver com a causa.
        if (pacote?.Link is not { Length: > 0 } || pacote.Checksum is not { Length: > 0 }
                                                || pacote.Name is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                $"O Adoptium não tem JRE {majorVersion} para {SistemaOperativo()}/{Arquitectura()}.");
        }

        return pacote;
    }

    /// <summary>
    ///     Traz o arquivo para um temporário, verificando o hash NO MESMO passo.
    ///     Verificar depois exigiria ler os 45 MB outra vez; e não verificar
    ///     deixaria um download truncado virar um JRE meio extraído que falha
    ///     mais tarde, longe daqui.
    /// </summary>
    private async Task<string> BaixarAsync(
        AdoptiumPackage pacote,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(paths.RuntimesDirectory);

        var temporario = Path.Combine(paths.RuntimesDirectory, $"{Guid.CreateVersion7():N}.download");

        using var resposta = await http.GetAsync(
            pacote.Link, HttpCompletionOption.ResponseHeadersRead, ct);

        resposta.EnsureSuccessStatusCode();

        // O tamanho vem do índice, não do Content-Length: atrás de um proxy que
        // recomprime, o cabeçalho mente e a barra de progresso passa de 100%.
        var total = pacote.Size > 0 ? pacote.Size : resposta.Content.Headers.ContentLength ?? 0;

        using var sha = SHA256.Create();

        await using (var origem = await resposta.Content.ReadAsStreamAsync(ct))
        await using (var destino = File.Create(temporario))
        {
            var buffer = new byte[81920];
            long lidos = 0;
            int n;

            while ((n = await origem.ReadAsync(buffer, ct)) > 0)
            {
                sha.TransformBlock(buffer, 0, n, null, 0);
                await destino.WriteAsync(buffer.AsMemory(0, n), ct);

                lidos += n;

                if (total > 0)
                    progress?.Report((double)lidos / total);
            }

            sha.TransformFinalBlock([], 0, 0);
        }

        var obtido = Convert.ToHexStringLower(sha.Hash!);

        if (!obtido.Equals(pacote.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(temporario);

            // Não é paranoia: este ficheiro vira um executável que corremos na
            // máquina do jogador. Aceitar bytes que não batem seria correr o que
            // quer que tenha chegado pelo caminho.
            throw new InvalidOperationException(
                $"O JRE baixado não corresponde ao checksum publicado pelo Adoptium ({pacote.Name}).");
        }

        return temporario;
    }

    /// <summary>
    ///     Extrai para um temporário e só então move para o lugar definitivo.
    ///     Uma queda a meio da extração deixaria uma pasta com metade de um JRE —
    ///     e o <see cref="Localizar" /> a daria por boa, porque o <c>bin/java</c>
    ///     sai cedo no arquivo. O jogo abriria e morreria por falta de uma
    ///     biblioteca, sem nada a apontar para aqui.
    /// </summary>
    private static void ExtrairParaDestino(string arquivo, string nome, string destino)
    {
        var temporario = destino + ".tmp";

        if (Directory.Exists(temporario))
            Directory.Delete(temporario, true);

        Directory.CreateDirectory(temporario);

        if (nome.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(arquivo, temporario);
        }
        else
        {
            using var comprimido = File.OpenRead(arquivo);
            using var gzip = new GZipStream(comprimido, CompressionMode.Decompress);

            TarFile.ExtractToDirectory(gzip, temporario, overwriteFiles: true);
        }

        if (Directory.Exists(destino))
            Directory.Delete(destino, true);

        Directory.Move(temporario, destino);
    }

    private static string SistemaOperativo()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "mac" : "linux";
    }

    private static string Arquitectura() => RuntimeInformation.OSArchitecture switch
    {
        Architecture.Arm64 => "aarch64",
        Architecture.X86 => "x86",
        _ => "x64"
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "JRE {Major} removido por não ser usado.")]
    private partial void LogRemovido(int major);

    [LoggerMessage(Level = LogLevel.Information, Message = "Baixando JRE {Major} ({Pacote}).")]
    private partial void LogBaixando(int major, string pacote);
}
