using System.IO.Compression;
using TCMine_Application.Contracts;
using TCMine_Application.Modpack;
using TCMine_Domain.Modpack;
using Xunit;

namespace TCMine_Tests.Modpack;

// Helpers PUROS do importador CurseForge (sem tocar na API). São a base do import/add compartilhado
// por servidor e launcher.
public class CurseForgeImporterTests
{
    private static CfFileRefDto File(long id, string fileName, string? downloadUrl) =>
        new(id, ModId: 100, fileName, downloadUrl, DisplayName: fileName);

    // ── InferSide ────────────────────────────────────────────────────────────────────────────────
    // Regra: manifesto = mods do cliente; server pack = subconjunto do servidor. Presente ⇒ Both.

    [Fact]
    public void InferSide_sem_server_pack_assume_Both()
    {
        Assert.Equal(ModSide.Both, CurseForgeImporter.InferSide("a.jar", null));
    }

    [Fact]
    public void InferSide_presente_no_server_pack_e_Both()
    {
        var pack = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a.jar" };
        Assert.Equal(ModSide.Both, CurseForgeImporter.InferSide("a.jar", pack));
    }

    [Fact]
    public void InferSide_ausente_do_server_pack_e_Client()
    {
        var pack = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "outro.jar" };
        Assert.Equal(ModSide.Client, CurseForgeImporter.InferSide("a.jar", pack));
    }

    // ── ClassToTarget ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(12, "resourcepack")]
    [InlineData(6552, "shaderpack")]
    [InlineData(6, "mod")]      // classe de mods
    [InlineData(0, "mod")]      // desconhecida cai no default
    [InlineData(9999, "mod")]
    public void ClassToTarget_mapeia_classe_para_pasta(long classId, string expected)
    {
        Assert.Equal(expected, CurseForgeImporter.ClassToTarget(classId));
    }

    // ── ResolveDownloadUrl ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveDownloadUrl_null_devolve_null()
    {
        Assert.Null(CurseForgeImporter.ResolveDownloadUrl(null));
    }

    [Fact]
    public void ResolveDownloadUrl_usa_a_url_direta_quando_presente()
    {
        var f = File(123, "mod.jar", "https://cdn.example/mod.jar");
        Assert.Equal("https://cdn.example/mod.jar", CurseForgeImporter.ResolveDownloadUrl(f));
    }

    [Fact]
    public void ResolveDownloadUrl_reconstroi_url_edge_quando_download_nulo()
    {
        // id 3456789 → files/3456/789/<nome> (id/1000 e id%1000)
        var f = File(3456789, "mod.jar", null);
        Assert.Equal(
            "https://edge.forgecdn.net/files/3456/789/mod.jar",
            CurseForgeImporter.ResolveDownloadUrl(f));
    }

    [Fact]
    public void ResolveDownloadUrl_sem_url_e_sem_nome_devolve_null()
    {
        var f = File(3456789, "", null);
        Assert.Null(CurseForgeImporter.ResolveDownloadUrl(f));
    }

    // ── BuildOverridesZip ────────────────────────────────────────────────────────────────────────

    private static byte[] ZipWith(params (string Path, string Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var (path, content) in entries)
            {
                var e = zip.CreateEntry(path);
                using var s = new StreamWriter(e.Open());
                s.Write(content);
            }

        return ms.ToArray();
    }

    [Fact]
    public void BuildOverridesZip_remove_o_prefixo_da_pasta()
    {
        var src = new ZipArchive(new MemoryStream(ZipWith(
            ("overrides/config/a.toml", "x"),
            ("overrides/options.txt", "y"),
            ("manifest.json", "{}"))), ZipArchiveMode.Read);

        var bytes = CurseForgeImporter.BuildOverridesZip(src, "overrides");
        Assert.NotNull(bytes);

        using var outZip = new ZipArchive(new MemoryStream(bytes!), ZipArchiveMode.Read);
        var names = outZip.Entries.Select(e => e.FullName).OrderBy(n => n).ToList();
        // Sem o prefixo "overrides/", e o manifest.json (fora da pasta) não entra
        Assert.Equal(["config/a.toml", "options.txt"], names);
    }

    [Fact]
    public void BuildOverridesZip_sem_arquivos_na_pasta_devolve_null()
    {
        var src = new ZipArchive(new MemoryStream(ZipWith(
            ("manifest.json", "{}"))), ZipArchiveMode.Read);

        Assert.Null(CurseForgeImporter.BuildOverridesZip(src, "overrides"));
    }
}
