using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Infrastructure;
using TCMine.Launcher.Infrastructure.Instances;

namespace TCMine.Launcher.Core.Tests.Infrastructure;

/// <summary>
///     As pastas de instância, contra o disco de verdade.
///     Esta classe é a que APAGA arquivos no computador do jogador, então o que
///     ela recusa a fazer importa tanto quanto o que ela faz.
/// </summary>
public sealed class FileSystemInstanceStoreTests : IDisposable
{
    private readonly InstanceKey _chave = InstanceKey.New();

    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(), "tcmine-inst-" + Guid.NewGuid().ToString("N")[..8]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, true);
    }

    [Fact]
    public async Task Sem_manifesto_a_instancia_e_desconhecida() =>
        (await Criar().ReadManifestAsync(_chave, Ct)).ShouldBeNull();

    [Fact]
    public async Task Grava_e_le_o_manifesto_de_volta()
    {
        var store = Criar();
        var manifest = Manifesto(new Dictionary<string, string> { ["mods/jei.jar"] = "aa" });

        await store.WriteManifestAsync(_chave, manifest, Ct);

        var lido = await store.ReadManifestAsync(_chave, Ct);

        lido.ShouldNotBeNull();
        lido.ManagedFiles.ShouldBe(manifest.ManagedFiles);
        lido.ModpackName.ShouldBe("Pack");
    }

    [Fact]
    public async Task Manifesto_corrompido_e_tratado_como_ausente()
    {
        // A consequência é deliberada: o diff seguinte vê uma instância vazia,
        // baixa tudo de novo e NÃO apaga nada — porque sem conjunto gerenciado
        // não há o que apagar. Perder disco é aceitável; perder o mundo não.
        var store = Criar();
        var folder = store.PathFor(_chave);
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, InstanceManifest.FileName), "{ lixo", Ct);

        (await store.ReadManifestAsync(_chave, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Apaga_os_arquivos_pedidos_e_so_eles()
    {
        var store = Criar();
        var folder = store.PathFor(_chave);

        await EscreverAsync(folder, "mods/velho.jar");
        await EscreverAsync(folder, "saves/mundo/level.dat");
        await EscreverAsync(folder, "options.txt");

        await store.DeleteFilesAsync(_chave, ["mods/velho.jar"], Ct);

        File.Exists(Path.Combine(folder, "mods", "velho.jar")).ShouldBeFalse();
        File.Exists(Path.Combine(folder, "saves", "mundo", "level.dat")).ShouldBeTrue("o mundo é do jogador");
        File.Exists(Path.Combine(folder, "options.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task Caminho_que_escapa_da_pasta_e_ignorado()
    {
        // "Nunca deveria acontecer" não é garantia, e um ".." aqui apagaria
        // arquivos fora da instância.
        var store = Criar();
        var fora = Path.Combine(_raiz, "instances", "nao-toque.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(fora)!);
        await File.WriteAllTextAsync(fora, "importante", Ct);

        await store.DeleteFilesAsync(_chave, [Path.Combine("..", "nao-toque.txt")], Ct);

        File.Exists(fora).ShouldBeTrue();
    }

    [Fact]
    public async Task Pasta_que_ficou_vazia_e_removida()
    {
        // Sem isto a instância acumula esqueletos de versões antigas para sempre.
        var store = Criar();
        var folder = store.PathFor(_chave);
        await EscreverAsync(folder, "config/antigo/coisa.toml");

        await store.DeleteFilesAsync(_chave, [Path.Combine("config", "antigo", "coisa.toml")], Ct);

        Directory.Exists(Path.Combine(folder, "config", "antigo")).ShouldBeFalse();
        Directory.Exists(folder).ShouldBeTrue("a raiz da instância fica");
    }

    [Fact]
    public async Task A_listagem_ignora_pasta_sem_manifesto()
    {
        // Sobra de uma instalação interrompida. Listá-la ofereceria ao jogador um
        // card sem nome nem versão.
        var store = Criar();
        Directory.CreateDirectory(Path.Combine(_raiz, "instances", "sobra-de-instalacao"));

        await store.WriteManifestAsync(_chave, Manifesto([]), Ct);

        var instaladas = await store.ListAsync(Ct);

        instaladas.ShouldHaveSingleItem().Manifest.ModpackName.ShouldBe("Pack");
    }

    [Fact]
    public async Task Remover_leva_a_pasta_inteira()
    {
        var store = Criar();
        await EscreverAsync(store.PathFor(_chave), "saves/mundo/level.dat");
        await store.WriteManifestAsync(_chave, Manifesto([]), Ct);

        await store.RemoveAsync(_chave, Ct);

        Directory.Exists(store.PathFor(_chave)).ShouldBeFalse();
    }

    private FileSystemInstanceStore Criar() =>
        new(new LauncherPaths(_raiz), NullLogger<FileSystemInstanceStore>.Instance);

    private static InstanceManifest Manifesto(Dictionary<string, string> files) => new()
    {
        Schema = 1,
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ModpackName = "Pack",
        Version = "1.0.0",
        InstalledAt = DateTimeOffset.UtcNow,
        ManagedFiles = files
    };

    private static async Task EscreverAsync(string raiz, string relativo)
    {
        var path = Path.Combine(raiz, relativo.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "conteudo", Ct);
    }
}
