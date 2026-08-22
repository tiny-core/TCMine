using System.Text;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Edição de modpack, capa e novidades.
///     São os casos de uso mais simples do sistema, e é por isso que estavam
///     sem teste. O que se trava aqui não é a lógica — é o contorno: o que NÃO
///     pode ser editado, o que acontece com espaço em branco, e o que responde
///     quando o alvo não existe.
/// </summary>
public sealed class ModpackCrudTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Editar_modpack_nao_toca_no_que_e_imutavel()
    {
        // Slug, versão do Minecraft e loader são fixados na criação: mods não
        // migram entre versões de MC nem entre loaders, e deixar isso mudar
        // depois quebraria todo servidor já apontado para o pack.
        var repo = new FakeModpacks();
        var antes = (repo.Modpack.Slug, repo.Modpack.MinecraftVersion, repo.Modpack.Loader);

        var result = await new UpdateModpack(repo)
            .HandleAsync(repo.Modpack.Id, "  Nome Novo  ", "  resumo  ", Ct);

        result.Succeeded.ShouldBeTrue();
        repo.Modpack.Name.ShouldBe("Nome Novo");
        repo.Modpack.Summary.ShouldBe("resumo");

        (repo.Modpack.Slug, repo.Modpack.MinecraftVersion, repo.Modpack.Loader).ShouldBe(antes);
    }

    [Fact]
    public async Task Editar_modpack_recusa_nome_em_branco()
    {
        var repo = new FakeModpacks();

        var result = await new UpdateModpack(repo).HandleAsync(repo.Modpack.Id, "   ", null, Ct);

        result.Succeeded.ShouldBeFalse();
        repo.Modpack.Name.ShouldBe("Original");
    }

    [Fact]
    public async Task Resumo_em_branco_vira_nulo_e_nao_string_vazia()
    {
        // A UI decide exibir ou não pelo nulo; string vazia renderizaria um
        // parágrafo em branco no lugar do resumo.
        var repo = new FakeModpacks();

        await new UpdateModpack(repo).HandleAsync(repo.Modpack.Id, "Nome", "   ", Ct);

        repo.Modpack.Summary.ShouldBeNull();
    }

    [Fact]
    public async Task Editar_modpack_inexistente_e_recusado()
    {
        var result = await new UpdateModpack(new FakeModpacks())
            .HandleAsync(Guid.CreateVersion7(), "Nome", null, Ct);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Trocar_a_capa_so_muda_o_ponteiro()
    {
        // O blob antigo continua no store: ele pode estar compartilhado com
        // outro modpack, e apagá-lo aqui deixaria a capa alheia quebrada. A
        // garantia é estrutural — o IBlobStore nem expõe remoção; a limpeza de
        // órfãos é tarefa à parte, com varredura própria.
        var repo = new FakeModpacks();
        var anterior = new string('a', 64);
        repo.Modpack.IconBlobSha256 = anterior;

        var blobs = new FakeBlobs();
        var result = await new SetModpackIcon(repo, blobs)
            .HandleAsync(repo.Modpack.Id, Imagem(), "image/png", Ct);

        result.Succeeded.ShouldBeTrue();
        repo.Modpack.IconBlobSha256.ShouldBe(blobs.Gravado);
        repo.Modpack.IconBlobSha256.ShouldNotBe(anterior);
    }

    [Fact]
    public async Task Capa_de_modpack_inexistente_nao_grava_blob()
    {
        var blobs = new FakeBlobs();

        var result = await new SetModpackIcon(new FakeModpacks(), blobs)
            .HandleAsync(Guid.CreateVersion7(), Imagem(), "image/png", Ct);

        result.Succeeded.ShouldBeFalse();
        blobs.Gravado.ShouldBeNull();
    }

    [Fact]
    public async Task Novidade_nasce_com_o_titulo_aparado()
    {
        var repo = new FakeNews();

        var result = await new CreateNews(repo)
            .HandleAsync(Guid.CreateVersion7(), "  Servidor no ar  ", "corpo", true, Ct);

        result.Succeeded.ShouldBeTrue();
        repo.Adicionada!.Title.ShouldBe("Servidor no ar");
        repo.Adicionada.IsPublished.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Novidade_sem_titulo_e_recusada(string titulo)
    {
        var repo = new FakeNews();

        var result = await new CreateNews(repo)
            .HandleAsync(Guid.CreateVersion7(), titulo, "corpo", false, Ct);

        result.Succeeded.ShouldBeFalse();
        repo.Adicionada.ShouldBeNull();
    }

    [Fact]
    public async Task Despublicar_novidade_e_edicao_e_nao_remocao()
    {
        // Tirar do ar sem apagar: o admin costuma querer o texto de volta, e
        // reescrevê-lo do zero seria a alternativa.
        var repo = new FakeNews(Novidade(publicada: true));

        var result = await new UpdateNews(repo)
            .HandleAsync(repo.Existente!.Id, "Título", "corpo", false, Ct);

        result.Succeeded.ShouldBeTrue();
        repo.Existente.IsPublished.ShouldBeFalse();
        repo.Removida.ShouldBeNull();
    }

    [Fact]
    public async Task Editar_novidade_inexistente_e_recusado()
    {
        var result = await new UpdateNews(new FakeNews())
            .HandleAsync(Guid.CreateVersion7(), "Título", "corpo", true, Ct);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Apagar_novidade_remove_pelo_id()
    {
        var repo = new FakeNews(Novidade(publicada: true));

        var result = await new DeleteNews(repo).HandleAsync(repo.Existente!.Id, Ct);

        result.Succeeded.ShouldBeTrue();
        repo.Removida.ShouldBe(repo.Existente.Id);
    }

    private static MemoryStream Imagem() => new(Encoding.UTF8.GetBytes("png"));

    private static News Novidade(bool publicada) => new()
    {
        ModpackId = Guid.CreateVersion7(),
        Title = "Título",
        Body = "corpo",
        IsPublished = publicada
    };

    private sealed class FakeModpacks : FakeModpackRepositoryBase
    {
        public Modpack Modpack { get; } = new()
        {
            Slug = "teste",
            Name = "Original",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == Modpack.Id ? Modpack : null);

        public override Task UpdateAsync(Modpack modpack, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeBlobs : FakeBlobStoreBase
    {
        public string? Gravado { get; private set; }

        public override Task<string> PutAsync(
            Stream content, string? expectedSha256, string? contentType, CancellationToken ct)
        {
            Gravado = new string('b', 64);
            return Task.FromResult(Gravado);
        }
    }

    private sealed class FakeNews(News? existente = null) : INewsRepository
    {
        public News? Existente { get; } = existente;
        public News? Adicionada { get; private set; }
        public Guid? Removida { get; private set; }

        public Task<IReadOnlyList<News>> ListByModpackAsync(Guid modpackId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<News>>(Existente is null ? [] : [Existente]);

        public Task<News?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Existente?.Id == id ? Existente : null);

        public Task AddAsync(News news, CancellationToken ct)
        {
            Adicionada = news;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(News news, CancellationToken ct) => Task.CompletedTask;

        public Task RemoveAsync(Guid id, CancellationToken ct)
        {
            Removida = id;
            return Task.CompletedTask;
        }
    }
}
