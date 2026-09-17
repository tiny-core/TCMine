using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Launcher.Core.Tests.Fakes;
using TCMine.Launcher.Infrastructure;
using TCMine.Launcher.Infrastructure.Runtime;

namespace TCMine.Launcher.Core.Tests.Runtime;

/// <summary>
///     O download e a guarda do JRE.
///     O que se exercita aqui é o caminho inteiro com bytes de verdade: índice,
///     download, verificação do hash, extração e localização do executável. Um
///     zip montado em memória é suficiente e mantém o teste sem rede — e foi o
///     único jeito de cobrir a parte que mais assusta, que é correr na máquina do
///     jogador um binário vindo da internet.
/// </summary>
public class AdoptiumJavaLocatorTests : IDisposable
{
    private const string Download = "https://exemplo/jre.zip";

    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(), $"tcmine-java-{Guid.CreateVersion7():N}");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>O nome que o locator procura, que difere por sistema.</summary>
    private static string Executavel =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, true);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Baixa_verifica_e_devolve_o_executavel()
    {
        var zip = MontarZip();
        var handler = HandlerCom(zip, Sha256De(zip));

        var caminho = await Montar(handler).EnsureRuntimeAsync(21, null, Ct);

        File.Exists(caminho).ShouldBeTrue();
        caminho.ShouldEndWith(Path.Combine("bin", Executavel));
    }

    [Fact]
    public async Task Checksum_que_nao_bate_recusa_e_nao_deixa_nada_para_tras()
    {
        // Este ficheiro vira um executável que corremos na máquina do jogador.
        // Aceitar bytes que não batem seria correr o que quer que tenha chegado.
        var handler = HandlerCom(MontarZip(), new string('a', 64));

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await Montar(handler).EnsureRuntimeAsync(21, null, Ct));

        // Nem o .download temporário nem um JRE meio extraído sobrevivem: o
        // arranque seguinte tem de tentar de novo, não achar lixo pela frente.
        Directory.EnumerateFileSystemEntries(Path.Combine(_raiz, "runtimes"))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Ja_instalado_nao_toca_na_rede()
    {
        // Caminho de todo arranque depois do primeiro, e o jogador acabou de
        // clicar em jogar: uma ida à rede aqui seria espera por nada.
        var bin = Path.Combine(_raiz, "runtimes", "21", "jdk-21.0.5+11-jre", "bin");

        Directory.CreateDirectory(bin);
        await File.WriteAllTextAsync(Path.Combine(bin, Executavel), "", Ct);

        var handler = new FakeHttpHandler();

        var caminho = await Montar(handler).EnsureRuntimeAsync(21, null, Ct);

        caminho.ShouldBe(Path.Combine(bin, Executavel));
        handler.Pedidos.ShouldBeEmpty();
    }

    [Fact]
    public async Task O_progresso_vai_de_zero_a_um()
    {
        var zip = MontarZip();
        var relatado = new List<double>();

        await Montar(HandlerCom(zip, Sha256De(zip)))
            .EnsureRuntimeAsync(21, new Progress<double>(relatado.Add), Ct);

        // Progress<T> despacha no contexto de sincronização; sem esperar, a lista
        // pode estar vazia num teste que passou pelo download inteiro.
        await Task.Delay(50, Ct);

        relatado.ShouldNotBeEmpty();
        relatado[^1].ShouldBe(1, 0.001);
    }

    [Fact]
    public async Task Sem_build_para_esta_plataforma_falha_com_a_combinacao_no_texto()
    {
        // Falhar aqui, dizendo sistema e arquitetura, poupa investigar um erro de
        // extração mais à frente que não teria nada a ver com a causa.
        var handler = new FakeHttpHandler();
        handler.Responde(IndiceUrl(21), System.Net.HttpStatusCode.OK, Array.Empty<object>());

        var erro = await Should.ThrowAsync<InvalidOperationException>(
            async () => await Montar(handler).EnsureRuntimeAsync(21, null, Ct));

        erro.Message.ShouldContain("JRE 21");
    }

    // ---------- apoio ----------

    private AdoptiumJavaLocator Montar(FakeHttpHandler handler) =>
        new(new HttpClient(handler), new LauncherPaths(_raiz), NullLogger<AdoptiumJavaLocator>.Instance);

    private static string IndiceUrl(int major) =>
        $"https://api.adoptium.net/v3/assets/latest/{major}/hotspot?image_type=jre&vendor=eclipse"
        + $"&os={(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "mac" : "linux")}"
        + $"&architecture={RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "aarch64",
            Architecture.X86 => "x86",
            _ => "x64"
        }}";

    private static FakeHttpHandler HandlerCom(byte[] zip, string checksum)
    {
        var handler = new FakeHttpHandler();

        // Nomes em minúscula de propósito: é assim que o Adoptium responde, e o
        // contexto source-gen do locator espera camelCase.
        handler.Responde(IndiceUrl(21), System.Net.HttpStatusCode.OK, new[]
        {
            new
            {
                binary = new
                {
                    package = new
                    {
                        link = Download, checksum, name = "OpenJDK21U-jre.zip", size = (long)zip.Length
                    }
                }
            }
        });

        return handler.RespondeBytes(Download, zip);
    }

    /// <summary>
    ///     Um "JRE" com a forma que o Adoptium entrega: pasta raiz com o nome da
    ///     build, e o executável em bin/ dentro dela. É essa forma que o locator
    ///     procura, e por isso é ela que o teste precisa reproduzir.
    /// </summary>
    private static byte[] MontarZip()
    {
        using var memoria = new MemoryStream();

        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
        {
            zip.CreateEntry($"jdk-21.0.5+11-jre/bin/{Executavel}");
            zip.CreateEntry("jdk-21.0.5+11-jre/lib/modules");
        }

        return memoria.ToArray();
    }

    private static string Sha256De(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
