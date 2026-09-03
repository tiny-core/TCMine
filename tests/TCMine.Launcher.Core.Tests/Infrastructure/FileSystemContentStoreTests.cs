using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Infrastructure;
using TCMine.Launcher.Infrastructure.Content;

namespace TCMine.Launcher.Core.Tests.Infrastructure;

/// <summary>
///     O store endereçado por conteúdo, contra o disco de verdade.
///     Contra o disco porque é o disco que quebra: o hash é conferido enquanto o
///     arquivo é gravado, e o que separa "conteúdo bom" de "conteúdo adulterado"
///     é justamente essa gravação.
/// </summary>
public sealed class FileSystemContentStoreTests : IDisposable
{
    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(), "tcmine-store-" + Guid.NewGuid().ToString("N")[..8]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, true);
    }

    [Fact]
    public async Task Guarda_e_reconhece_o_conteudo()
    {
        var store = Criar();
        var (conteudo, sha) = Conteudo("um mod qualquer");

        (await store.ContainsAsync(sha, Ct)).ShouldBeFalse();

        await store.AddAsync(sha, new MemoryStream(conteudo), Ct);

        (await store.ContainsAsync(sha, Ct)).ShouldBeTrue();
        (await store.ListHashesAsync(Ct)).ShouldContain(sha);
    }

    [Fact]
    public async Task Conteudo_que_nao_confere_com_o_hash_e_recusado()
    {
        // O arquivo pode ter chegado corrompido ou adulterado. Aceitar aqui
        // significaria servir o conteúdo errado para sempre, porque daqui em
        // diante ninguém mais confere.
        var store = Criar();
        var (_, shaEsperado) = Conteudo("o que o manifesto prometeu");
        var outro = Encoding.UTF8.GetBytes("o que chegou de verdade");

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.AddAsync(shaEsperado, new MemoryStream(outro), Ct));

        (await store.ContainsAsync(shaEsperado, Ct)).ShouldBeFalse("nada pode ter sido guardado");
    }

    [Fact]
    public async Task Recusa_nao_deixa_lixo_temporario()
    {
        // O .tmp abandonado seria contado pelo ListHashes se tivesse 64
        // caracteres, e um download necessário seria pulado.
        var store = Criar();
        var (_, sha) = Conteudo("prometido");

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.AddAsync(sha, new MemoryStream("outro"u8.ToArray()), Ct));

        Directory.EnumerateFiles(_raiz, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task Guardar_duas_vezes_o_mesmo_conteudo_e_operacao_normal()
    {
        // Dois modpacks com o mesmo mod chegam aqui com o mesmo hash.
        var store = Criar();
        var (conteudo, sha) = Conteudo("compartilhado");

        await store.AddAsync(sha, new MemoryStream(conteudo), Ct);
        await store.AddAsync(sha, new MemoryStream(conteudo), Ct);

        (await store.ListHashesAsync(Ct)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Materializa_copiando_quando_nao_ha_hardlink()
    {
        var store = Criar();
        var (conteudo, sha) = Conteudo("jar");
        await store.AddAsync(sha, new MemoryStream(conteudo), Ct);

        var destino = Path.Combine(_raiz, "instancia", "mods", "jei.jar");

        await store.MaterializeAsync(sha, destino, allowHardLink: false, Ct);

        (await File.ReadAllBytesAsync(destino, Ct)).ShouldBe(conteudo);
    }

    [Fact]
    public async Task Materializar_por_cima_apaga_antes_de_escrever()
    {
        // Sobrescrever um hardlink existente escreveria NO BLOB, e a corrupção
        // viajaria para todas as instâncias que o compartilham.
        var store = Criar();
        var (conteudo, sha) = Conteudo("novo");
        await store.AddAsync(sha, new MemoryStream(conteudo), Ct);

        var destino = Path.Combine(_raiz, "instancia", "mods", "jei.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await File.WriteAllTextAsync(destino, "versão antiga bem mais longa", Ct);

        await store.MaterializeAsync(sha, destino, allowHardLink: false, Ct);

        (await File.ReadAllBytesAsync(destino, Ct)).ShouldBe(conteudo);
    }

    [Fact]
    public async Task Materializar_conteudo_ausente_falha_em_vez_de_criar_arquivo_vazio()
    {
        var store = Criar();

        await Should.ThrowAsync<FileNotFoundException>(async () =>
            await store.MaterializeAsync(new string('a', 64), Path.Combine(_raiz, "x.jar"), false, Ct));
    }

    [Fact]
    public async Task Usa_o_hardlink_quando_o_sistema_deixa()
    {
        var linker = new LinkerQueRegistra();
        var store = new FileSystemContentStore(
            new LauncherPaths(_raiz), linker, NullLogger<FileSystemContentStore>.Instance);

        var (conteudo, sha) = Conteudo("jar");
        await store.AddAsync(sha, new MemoryStream(conteudo), Ct);

        var destino = Path.Combine(_raiz, "instancia", "mods", "jei.jar");

        await store.MaterializeAsync(sha, destino, allowHardLink: true, Ct);

        linker.Chamadas.ShouldHaveSingleItem();
    }

    private FileSystemContentStore Criar() =>
        new(new LauncherPaths(_raiz), new NoFileLinker(), NullLogger<FileSystemContentStore>.Instance);

    private static (byte[] Bytes, string Sha) Conteudo(string texto)
    {
        var bytes = Encoding.UTF8.GetBytes(texto);

        return (bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    /// <summary>Liga de verdade, copiando: o teste quer saber se foi CHAMADO.</summary>
    private sealed class LinkerQueRegistra : IFileLinker
    {
        public List<string> Chamadas { get; } = [];

        public bool TryCreateHardLink(string existingPath, string newLinkPath)
        {
            Chamadas.Add(newLinkPath);
            File.Copy(existingPath, newLinkPath);

            return true;
        }
    }
}
