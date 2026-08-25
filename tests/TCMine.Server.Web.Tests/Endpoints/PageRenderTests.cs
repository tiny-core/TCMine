using Microsoft.Extensions.DependencyInjection;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Toda página do painel renderiza.
///     Existe por um erro que build e testes não pegam: um parâmetro que o
///     componente não tem. O Razor aceita, o compilador aceita, e a página
///     estoura ao RENDERIZAR — com 500 no servidor e, dentro de um grid, um
///     spinner que gira para sempre, porque o MudDataGrid não tem estado de
///     erro. A aba de Recursos foi ao ar assim.
///     O caso da lista VAZIA importa tanto quanto o cheio: o
///     <c>NoRecordsContent</c> só é construído quando não há linhas, então um
///     erro ali fica invisível enquanto houver dados.
///     Afirma que a página RESPONDE, e não como ela é: assim a rede continua
///     valendo enquanto o layout muda. Um teste amarrado à marcação viraria
///     peso na primeira remodelação da interface.
/// </summary>
public sealed class PageRenderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> Abas => new() { "mods", "recursos", "overrides" };

    /// <summary>
    ///     Rotas que não dependem de um modpack. Vale a lista inteira: um erro
    ///     de renderização não escolhe página, e o custo de cobrir todas é uma
    ///     linha por rota.
    /// </summary>
    public static TheoryData<string> Rotas => new()
    {
        "/", "/modpacks", "/mods", "/servers", "/storage", "/settings",
        "/login", "/forgot-password"
    };

    /// <summary>Abas do modpack que não são por versão.</summary>
    public static TheoryData<string> AbasDoModpack => new() { "", "/news", "/servers" };

    [Theory]
    [MemberData(nameof(Rotas))]
    public async Task Rota_renderiza(string rota)
    {
        await using var factory = new TcMineAppFactory();

        var html = await BuscarAsync(factory, rota);

        html.ShouldNotBeNull($"{rota} respondeu com erro");
    }

    [Theory]
    [MemberData(nameof(AbasDoModpack))]
    public async Task Aba_de_modpack_renderiza(string sufixo)
    {
        await using var factory = new TcMineAppFactory();
        var (modpackId, _) = await SemearAsync(factory, comArquivos: true);

        var html = await BuscarAsync(factory, $"/modpacks/{modpackId}{sufixo}");

        html.ShouldNotBeNull($"a aba /modpacks/id{sufixo} respondeu com erro");
    }

    [Fact]
    public async Task Modpack_sem_versao_nenhuma_renderiza()
    {
        // Um modpack recém-criado não tem versão, e o seletor e as abas por
        // versão precisam lidar com isso sem estourar.
        await using var factory = new TcMineAppFactory();
        var modpackId = await SemearModpackVazioAsync(factory);

        var html = await BuscarAsync(factory, $"/modpacks/{modpackId}");

        html.ShouldNotBeNull("um modpack sem versões respondeu com erro");
    }

    [Theory]
    [MemberData(nameof(Abas))]
    public async Task Aba_de_versao_com_conteudo_renderiza(string aba)
    {
        await using var factory = new TcMineAppFactory();
        var (modpackId, versionId) = await SemearAsync(factory, comArquivos: true);

        var html = await BuscarAsync(factory, $"/modpacks/{modpackId}/versions/{versionId}/{aba}");

        html.ShouldNotBeNull($"a aba /{aba} respondeu com erro");
    }

    [Theory]
    [MemberData(nameof(Abas))]
    public async Task Aba_de_versao_vazia_renderiza(string aba)
    {
        // Sem arquivo nenhum: é aqui que o NoRecordsContent entra em cena, e foi
        // exatamente ali que o parâmetro inexistente se escondeu.
        await using var factory = new TcMineAppFactory();
        var (modpackId, versionId) = await SemearAsync(factory, comArquivos: false);

        var html = await BuscarAsync(factory, $"/modpacks/{modpackId}/versions/{versionId}/{aba}");

        html.ShouldNotBeNull($"a aba /{aba} vazia respondeu com erro");
    }

    private static async Task<string?> BuscarAsync(TcMineAppFactory factory, string rota)
    {
        var cookie = await factory.EntrarComoAdminAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        var resposta = await client.GetAsync(rota, Ct);

        return resposta.IsSuccessStatusCode
            ? await resposta.Content.ReadAsStringAsync(Ct)
            : null;
    }

    private static async Task<Guid> SemearModpackVazioAsync(TcMineAppFactory factory)
    {
        using var escopo = factory.Services.CreateScope();
        var repo = escopo.ServiceProvider.GetRequiredService<IModpackRepository>();

        var modpack = new Modpack
        {
            Slug = $"vazio-{Guid.CreateVersion7():N}"[..18],
            Name = "Vazio",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        await repo.CreateAsync(modpack, Ct);
        return modpack.Id;
    }

    private static async Task<(Guid ModpackId, Guid VersionId)> SemearAsync(
        TcMineAppFactory factory, bool comArquivos)
    {
        using var escopo = factory.Services.CreateScope();
        var repo = escopo.ServiceProvider.GetRequiredService<IModpackRepository>();

        var modpack = new Modpack
        {
            Slug = $"pack-{Guid.CreateVersion7():N}"[..18],
            Name = "Pack",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        var version = new ModpackVersion
        {
            ModpackId = modpack.Id, Version = "1.0.0", LoaderVersion = "21.1.100"
        };

        if (comArquivos)
        {
            version.UpsertFile(Arquivo(version.Id, "mods/jei.jar", "jei"));
            version.UpsertFile(Arquivo(version.Id, "shaderpacks/complementary.zip", "shader"));
            version.UpsertFile(Arquivo(version.Id, "config/algo.toml", ModpackFile.OverrideSlug("config/algo.toml")));
        }

        await repo.CreateAsync(modpack, Ct);
        await repo.AddVersionAsync(version, Ct);

        return (modpack.Id, version.Id);
    }

    private static ModpackFile Arquivo(Guid versionId, string path, string slug) => new()
    {
        ModpackVersionId = versionId,
        Path = path,
        Sha256 = new string('a', 64),
        SizeBytes = 10,
        Side = FileSide.Both,
        Origin = path.StartsWith("config/", StringComparison.Ordinal)
            ? ModFileOrigin.Override
            : ModFileOrigin.CurseForge,
        ProjectSlug = slug
    };
}
