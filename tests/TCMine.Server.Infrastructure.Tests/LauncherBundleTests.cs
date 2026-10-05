using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Infrastructure.Launcher;
using TCMine.Server.Infrastructure.Versions;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     O launcher embutido na imagem é publicado no feed no arranque. As regras
///     que importam: um arranque comum não reempacota nada; um feed com versão
///     MAIOR (publicada à mão) não é rebaixado; e mudar o endereço refaz o canal,
///     porque o vpk recusa a mesma versão duas vezes.
/// </summary>
public sealed class LauncherBundleTests : IDisposable
{
    private const string Channel = "win-x64-p2";
    private readonly string _root = Directory.CreateTempSubdirectory("tcmine-feed-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData(null, null, LauncherBundleAction.Append)]                    // canal vazio
    [InlineData("0.9.0", null, LauncherBundleAction.Append)]                 // versão nova
    [InlineData("1.1.0", null, LauncherBundleAction.KeepNewer)]              // publicada à mão, maior
    [InlineData("1.0.0", "https://a/", LauncherBundleAction.Skip)]           // nada mudou
    [InlineData("1.0.0", "https://outro/", LauncherBundleAction.Rebuild)]    // endereço mudou
    [InlineData("1.0.0", null, LauncherBundleAction.Rebuild)]                // mesma versão, publicada à mão
    public void Decide_o_que_fazer_com_o_canal(string? published, string? stampUrl, LauncherBundleAction expected)
    {
        var stamp = stampUrl is null ? null : new LauncherBundleStamp("1.0.0", stampUrl);

        LauncherBundlePlan.Decide("1.0.0", "https://a/", published, stamp).ShouldBe(expected);
    }

    [Fact]
    public async Task Pagina_publica_acha_o_instalador_no_formato_real_do_vpk()
    {
        // Saída do vpk 1.2: o RELEASES-{canal} é texto (formato Squirrel) — o
        // código antigo o lia como JSON e nunca achava o instalador.
        var dir = Directory.CreateDirectory(Path.Combine(_root, Channel)).FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, $"RELEASES-{Channel}"),
            $"ABC TCMine.Launcher-1.0.0-{Channel}-full.nupkg 10", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(dir, $"releases.{Channel}.json"), $$"""
            {"Assets":[
              {"PackageId":"TCMine.Launcher","Version":"0.9.0","Type":"Full","FileName":"a.nupkg"},
              {"PackageId":"TCMine.Launcher","Version":"1.0.0","Type":"Delta","FileName":"b.nupkg"},
              {"PackageId":"TCMine.Launcher","Version":"1.0.0","Type":"Full","FileName":"c.nupkg"}]}
            """, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(dir, $"assets.{Channel}.json"), $$"""
            [{"RelativeFileName":"TCMine.Launcher-{{Channel}}-Setup.exe","Type":"Installer"},
             {"RelativeFileName":"c.nupkg","Type":"Full"}]
            """, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(dir, $"TCMine.Launcher-{Channel}-Setup.exe"), "x",
            TestContext.Current.CancellationToken);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LauncherUpdates:RootPath"] = _root })
            .Build();

        var latest = await new FileSystemLauncherReleaseSource(config).GetLatestAsync(TestContext.Current.CancellationToken);

        latest.ShouldNotBeNull();
        latest.Version.ShouldBe("1.0.0");
        latest.DownloadUrl.ShouldBe($"/updates/launcher/{Channel}/TCMine.Launcher-{Channel}-Setup.exe?v=1.0.0");
    }

    [Fact]
    public async Task Sem_launcher_na_imagem_nao_faz_nada()
    {
        var bundle = Bundle(Path.Combine(_root, "nao-existe"));

        (await bundle.PublishAsync(new Uri("https://a/"), TestContext.Current.CancellationToken))
            .ShouldBe(LauncherBundleOutcome.NoBundle);
    }

    /// <summary>
    ///     De ponta a ponta, com o vpk de verdade. Opt-in: precisa de uma pasta
    ///     no layout da imagem (app/, vpk/, VERSION) em TCMINE_TEST_LAUNCHER_BUNDLE
    ///     e do zstd no PATH — o CI não monta isso, a imagem sim.
    /// </summary>
    [Fact]
    public async Task Empacota_com_o_endereco_embutido_e_nao_refaz_sem_mudanca()
    {
        var bundlePath = Environment.GetEnvironmentVariable("TCMINE_TEST_LAUNCHER_BUNDLE");
        Assert.SkipWhen(string.IsNullOrEmpty(bundlePath), "TCMINE_TEST_LAUNCHER_BUNDLE não definido.");

        var ct = TestContext.Current.CancellationToken;
        var bundle = Bundle(bundlePath!);

        (await bundle.PublishAsync(new Uri("https://jogo.exemplo/"), ct)).ShouldBe(LauncherBundleOutcome.Published);
        (await bundle.PublishAsync(new Uri("https://jogo.exemplo/"), ct)).ShouldBe(LauncherBundleOutcome.UpToDate);

        var dir = Path.Combine(_root, Channel);
        VelopackFeed.InstallerFileName(dir, Channel).ShouldNotBeNull();

        // O server.json está DENTRO do pacote que o jogador instala.
        var nupkg = Directory.GetFiles(dir, "*-full.nupkg").ShouldHaveSingleItem();
        using var zip = System.IO.Compression.ZipFile.OpenRead(nupkg);
        var entry = zip.Entries.Single(e => e.Name == "server.json");
        using var reader = new StreamReader(entry.Open());
        (await reader.ReadToEndAsync(ct)).ShouldContain("https://jogo.exemplo/");

        // Endereço novo, mesma versão: o canal é refeito.
        (await bundle.PublishAsync(new Uri("https://novo.exemplo/"), ct)).ShouldBe(LauncherBundleOutcome.Published);
        LauncherBundleStamp.Read(dir)!.ServerUrl.ShouldBe("https://novo.exemplo/");
    }

    [Fact]
    public void Erro_do_vpk_chega_ao_log_e_nao_o_stack_trace()
    {
        // Saída real (encurtada) da primeira falha em produção: as últimas linhas
        // são o stack trace, e era só isso que o log mostrava.
        const string output = """
            [14:02:16 INF] Starting: Post-process steps
            [14:02:17 FTL] Access to the path '/feed/TCMine.Launcher-win-x64-p2-Setup.exe' is denied.
            System.UnauthorizedAccessException: Access to the path '/feed/TCMine.Launcher-win-x64-p2-Setup.exe' is denied.
               at Velopack.Packaging.PackageBuilder`2.RunCoreAsync(T options) in ./vpk/Velopack.Packaging/PackageBuilder.cs:line 113
               at Velopack.Core.Abstractions.ValidatedCommand`1.Run(TOpt options) in ./vpk/Velopack.Core/Abstractions/ValidatedCommand.cs:line 18
            """;

        VelopackLauncherBundle.VpkErrorSummary(output)
            .ShouldBe("[14:02:17 FTL] Access to the path '/feed/TCMine.Launcher-win-x64-p2-Setup.exe' is denied.");
    }

    [Fact]
    public async Task Feed_que_o_container_nao_consegue_escrever_falha_antes_do_vpk()
    {
        if (OperatingSystem.IsWindows())
            Assert.Skip("Permissão Unix.");

        var bundle = Directory.CreateDirectory(Path.Combine(_root, "bundle")).FullName;
        Directory.CreateDirectory(Path.Combine(bundle, "app"));
        await File.WriteAllTextAsync(Path.Combine(bundle, "VERSION"), "1.0.0", Ct);

        // O instalador de uma publicação manual, copiado como root.
        var channelDir = Directory.CreateDirectory(Path.Combine(_root, Channel)).FullName;
        var installer = Path.Combine(channelDir, $"TCMine.Launcher-{Channel}-Setup.exe");
        await File.WriteAllTextAsync(installer, "x", Ct);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(installer, UnixFileMode.UserRead);
        Assert.SkipUnless(VelopackFeed.FindUnwritable(channelDir) is not null,
            "Rodando como root: a permissão não se aplica.");

        // Sem vpk no pacote: se a conferência não viesse antes, o erro seria "vpk não encontrado".
        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => Bundle(bundle).PublishAsync(new Uri("https://jogo/"), Ct));

        error.Message.ShouldContain(installer);
        error.Message.ShouldContain("chown");
    }

    private VelopackLauncherBundle Bundle(string bundlePath) =>
        new(Options.Create(new LauncherBundleOptions { RootPath = _root, BundlePath = bundlePath }),
            NullLogger<VelopackLauncherBundle>.Instance);
}
