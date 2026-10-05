using System.Net;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     O feed de atualização do launcher.
///     O handshake aponta para ele desde sempre e nada o servia; desde que o
///     protocolo subiu, um launcher antigo é recusado e mandado atualizar por
///     aqui — então uma rota que não serve é pior do que não recusar.
/// </summary>
public sealed class LauncherUpdateEndpointsTests : IDisposable
{
    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(), $"tcmine-updates-{Guid.CreateVersion7():N}");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, true);
    }

    [Fact]
    public async Task Serve_o_ficheiro_do_canal_sem_exigir_sessao()
    {
        // Anónimo por necessidade: um launcher velho demais para autenticar é
        // exatamente o que precisa de se atualizar.
        await SemearAsync("win-x64-p2", "RELEASES", "conteudo do feed");

        using var factory = Build();

        var response = await factory.CreateClient()
            .GetAsync("/updates/launcher/win-x64-p2/RELEASES", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("conteudo do feed");
    }

    [Fact]
    public async Task Instalador_e_indices_nao_ficam_em_cache_e_o_pacote_fica()
    {
        // Nome fixo, conteúdo novo a cada versão: um CDN que o guardasse
        // entregava o Setup.exe antigo depois de a imagem publicar o novo.
        await SemearAsync("win-x64-p2", "TCMine.Launcher-win-x64-p2-Setup.exe", "setup");
        await SemearAsync("win-x64-p2", "TCMine.Launcher-1.0.0-win-x64-p2-full.nupkg", "pacote");

        using var factory = Build();
        var client = factory.CreateClient();

        var setup = await client.GetAsync("/updates/launcher/win-x64-p2/TCMine.Launcher-win-x64-p2-Setup.exe", Ct);
        var pacote = await client.GetAsync("/updates/launcher/win-x64-p2/TCMine.Launcher-1.0.0-win-x64-p2-full.nupkg", Ct);

        setup.Headers.CacheControl!.NoCache.ShouldBeTrue();
        pacote.Headers.CacheControl!.MaxAge.ShouldBe(TimeSpan.FromDays(365));
    }

    [Fact]
    public async Task Ficheiro_inexistente_da_404_e_nao_500()
    {
        await SemearAsync("win-x64-p2", "RELEASES", "x");

        using var factory = Build();

        var response = await factory.CreateClient()
            .GetAsync("/updates/launcher/win-x64-p2/nao-existe.nupkg", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Canal_desconhecido_da_404()
    {
        await SemearAsync("win-x64-p2", "RELEASES", "x");

        using var factory = Build();

        var response = await factory.CreateClient()
            .GetAsync("/updates/launcher/win-x64-p99/RELEASES", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/updates/launcher/win-x64-p2/..%2f..%2fappsettings.json")]
    [InlineData("/updates/launcher/..%2f..%2fetc/passwd")]
    public async Task Nao_serve_nada_fora_da_pasta(string rota)
    {
        // O canal e o ficheiro vêm da URL. Um ".." aqui serviria qualquer coisa
        // que o processo consiga ler — e este endpoint é anónimo.
        await SemearAsync("win-x64-p2", "RELEASES", "x");
        await File.WriteAllTextAsync(Path.Combine(_raiz, "..", "segredo.txt"), "nao devia sair", Ct);

        using var factory = Build();

        var response = await factory.CreateClient().GetAsync(rota, Ct);

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sem_pasta_configurada_a_rota_nem_existe()
    {
        // Uma instalação que ainda não publica launcher nenhum. Registar a rota
        // para ela responder 404 sempre seria prometer outra vez o que não há.
        using var factory = new TcMineAppFactory();

        var response = await factory.CreateClient()
            .GetAsync("/updates/launcher/win-x64-p2/RELEASES", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private TcMineAppFactory Build() =>
        new(settings: ("LauncherUpdates:RootPath", _raiz));

    private async Task SemearAsync(string canal, string file, string content)
    {
        var folder = Path.Combine(_raiz, canal);

        Directory.CreateDirectory(folder);

        await File.WriteAllTextAsync(Path.Combine(folder, file), content, Ct);
    }
}
