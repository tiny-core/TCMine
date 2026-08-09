using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     O merge é a peça que decide se atualizar um pack destrói o trabalho do
///     admin. Cada caso abaixo é uma forma diferente de destruir — e a garantia
///     de que não acontece.
/// </summary>
public sealed class UpstreamMergeTests
{
    [Fact]
    public void Aplica_atualizacao_do_autor_quando_ninguem_mexeu_aqui()
    {
        var plano = UpstreamMerge.Plan(
            Base(("jei", "v1")),
            Deles(("jei", "v2")),
            Nossos(("jei", "v1")));

        var mudanca = Assert.Single(plano.Update);
        Assert.Equal("jei", mudanca.ProjectId);
        Assert.Equal("v2", mudanca.ToFileId);
        Assert.Empty(plano.Conflicts);
    }

    [Fact]
    public void Mod_que_o_admin_acrescentou_nunca_e_removido()
    {
        // O caso que motivou tudo: "se eu tiver adicionado um mod extra, ao
        // atualizar não pode sobrepor o que eu já fiz".
        var plano = UpstreamMerge.Plan(
            Base(("jei", "v1")),
            Deles(("jei", "v1")),
            Nossos(("jei", "v1"), ("meu-mod", "x1")));

        Assert.Contains("meu-mod", plano.Kept);
        Assert.Empty(plano.Remove);
        Assert.Empty(plano.Conflicts);
    }

    [Fact]
    public void Autor_e_admin_mexendo_no_mesmo_mod_vira_conflito()
    {
        var plano = UpstreamMerge.Plan(
            Base(("jei", "v1")),
            Deles(("jei", "v3")),
            Nossos(("jei", "v2")));

        var conflito = Assert.Single(plano.Conflicts);
        Assert.Equal(UpstreamConflictKind.BothChanged, conflito.Kind);

        // Conflito nunca entra como atualização automática: o lado do admin fica.
        Assert.Empty(plano.Update);
    }

    [Fact]
    public void Autor_removendo_mod_intocado_acompanha_a_remocao()
    {
        var plano = UpstreamMerge.Plan(
            Base(("jei", "v1"), ("velho", "v1")),
            Deles(("jei", "v1")),
            Nossos(("jei", "v1"), ("velho", "v1")));

        var removido = Assert.Single(plano.Remove);
        Assert.Equal("velho", removido.ProjectId);
    }

    [Fact]
    public void Autor_removendo_mod_que_o_admin_trocou_vira_conflito()
    {
        var plano = UpstreamMerge.Plan(
            Base(("jei", "v1")),
            Deles(),
            Nossos(("jei", "v9")));

        var conflito = Assert.Single(plano.Conflicts);
        Assert.Equal(UpstreamConflictKind.ChangedHereRemovedThere, conflito.Kind);
        Assert.Empty(plano.Remove);
    }

    [Fact]
    public void Mod_que_o_admin_removeu_nao_volta_sozinho()
    {
        var plano = UpstreamMerge.Plan(
            Base(("chato", "v1")),
            Deles(("chato", "v2")),
            Nossos());

        var conflito = Assert.Single(plano.Conflicts);
        Assert.Equal(UpstreamConflictKind.RemovedHereKeptThere, conflito.Kind);

        // Se voltasse como "novo", toda atualização reinstalaria o que o admin
        // tirou de propósito.
        Assert.Empty(plano.Add);
    }

    [Fact]
    public void Mod_novo_do_autor_entra()
    {
        var plano = UpstreamMerge.Plan(
            Base(("jei", "v1")),
            Deles(("jei", "v1"), ("novidade", "v1")),
            Nossos(("jei", "v1")));

        var novo = Assert.Single(plano.Add);
        Assert.Equal("novidade", novo.ProjectId);
    }

    [Fact]
    public void Overrides_nao_contam_como_mod()
    {
        var arquivos = new List<ModpackFile>
        {
            File("jei", "v1"),
            new()
            {
                ModpackVersionId = Guid.Empty,
                ProjectSlug = "override:config/x.toml",
                Path = "config/x.toml",
                Sha256 = new string('a', 64),
                SizeBytes = 1,
                Side = FileSide.Both,
                Origin = ModFileOrigin.Override
            }
        };

        var plano = UpstreamMerge.Plan(Base(("jei", "v1")), Deles(("jei", "v1")), arquivos);

        // O override não pode aparecer como "mod que o admin adicionou", senão
        // toda versão importada acusaria milhares de acréscimos falsos.
        Assert.Empty(plano.Kept);
    }

    // ---- Fixtures ----

    private static UpstreamSnapshot Base(params (string Project, string File)[] mods) => new()
    {
        Mods = mods.ToDictionary(m => m.Project, m => m.File),
        Overrides = new Dictionary<string, string>()
    };

    private static Dictionary<string, string> Deles(params (string Project, string File)[] mods) =>
        mods.ToDictionary(m => m.Project, m => m.File);

    private static List<ModpackFile> Nossos(params (string Project, string File)[] mods) =>
        [.. mods.Select(m => File(m.Project, m.File))];

    private static ModpackFile File(string projectId, string fileId) => new()
    {
        ModpackVersionId = Guid.Empty,
        ProjectSlug = projectId,
        Path = $"mods/{projectId}.jar",
        Sha256 = new string('a', 64),
        SizeBytes = 1,
        Side = FileSide.Both,
        Origin = ModFileOrigin.CurseForge,
        OriginReference = fileId
    };
}
