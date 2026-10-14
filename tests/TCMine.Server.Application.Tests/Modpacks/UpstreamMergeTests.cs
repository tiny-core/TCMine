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
        var files = new List<ModpackFile>
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

        var plano = UpstreamMerge.Plan(Base(("jei", "v1")), Deles(("jei", "v1")), files);

        // O override não pode aparecer como "mod que o admin adicionou", senão
        // toda versão importada acusaria milhares de acréscimos falsos.
        Assert.Empty(plano.Kept);
    }

    [Fact]
    public void Config_removido_pelo_autor_sai_quando_o_admin_nao_tinha_mexido()
    {
        var plano = UpstreamMerge.Plan(
            Base([], [("config/a.toml", "sha-original")]),
            Deles(),
            [Override("config/a.toml", "sha-original")],
            new Dictionary<string, string>());

        Assert.Equal(["config/a.toml"], plano.Overrides.Removed);
        Assert.Empty(plano.Overrides.Conflicts);
    }

    [Fact]
    public void Config_removido_pelo_autor_fica_quando_o_admin_editou()
    {
        var plano = UpstreamMerge.Plan(
            Base([], [("config/a.toml", "sha-original")]),
            Deles(),
            [Override("config/a.toml", "sha-editado")],
            new Dictionary<string, string>());

        Assert.Empty(plano.Overrides.Removed);
        var conflito = Assert.Single(plano.Overrides.Conflicts);
        Assert.Equal("config/a.toml", conflito.Path);
        Assert.Equal(UpstreamConflictKind.ChangedHereRemovedThere, conflito.Kind);
    }

    [Fact]
    public void Mod_que_o_admin_removeu_e_o_autor_nao_mexeu_nao_vira_conflito()
    {
        // Sem isto, todo mod tirado de propósito voltava à tela a cada
        // atualização — e todo mod novo recusado uma vez, para sempre.
        var plano = UpstreamMerge.Plan(
            Base(("chato", "v1")),
            Deles(("chato", "v1")),
            Nossos());

        Assert.True(plano.IsEmpty);
    }

    [Fact]
    public void Config_editado_aqui_e_mudado_pelo_autor_vira_conflito_com_o_nosso_hash()
    {
        var plano = UpstreamMerge.Plan(
            Base([], [("config/a.toml", "sha-original")]),
            Deles(),
            [Override("config/a.toml", "sha-editado")],
            new Dictionary<string, string> { ["config/a.toml"] = "sha-do-autor" });

        var conflito = Assert.Single(plano.Overrides.Conflicts);
        Assert.Equal(UpstreamConflictKind.BothChanged, conflito.Kind);
        Assert.Equal("sha-editado", conflito.OurSha256);
        Assert.Empty(plano.Overrides.Updated);
    }

    [Fact]
    public void Config_que_o_admin_apagou_nao_volta_como_novo()
    {
        // Antes voltava como "config novo" e desfazia a remoção a cada
        // atualização do autor.
        var plano = UpstreamMerge.Plan(
            Base([], [("config/a.toml", "sha-original")]),
            Deles(),
            [],
            new Dictionary<string, string> { ["config/a.toml"] = "sha-do-autor" });

        Assert.Empty(plano.Overrides.Added);
        var conflito = Assert.Single(plano.Overrides.Conflicts);
        Assert.Equal(UpstreamConflictKind.RemovedHereKeptThere, conflito.Kind);
    }

    [Fact]
    public void Config_que_o_admin_editou_e_o_autor_nao_mexeu_nao_aparece()
    {
        var plano = UpstreamMerge.Plan(
            Base([], [("config/a.toml", "sha-original")]),
            Deles(),
            [Override("config/a.toml", "sha-editado")],
            new Dictionary<string, string> { ["config/a.toml"] = "sha-original" });

        Assert.True(plano.IsEmpty);
    }

    // ---- Escolha item a item ----

    [Fact]
    public void Escolha_padrao_aplica_o_que_nao_conflita_e_mantem_o_lado_do_admin()
    {
        var plano = UpstreamMerge.Plan(
            Base(("jei", "v1"), ("briga", "v1"), ("velho", "v1")),
            Deles(("jei", "v2"), ("briga", "v3"), ("novo", "n1")),
            Nossos(("jei", "v1"), ("briga", "v2"), ("velho", "v1")));

        var r = UpstreamMerge.Resolve(plano);

        Assert.Equal(["jei", "novo"], r.ModsToIngest.Select(c => c.ProjectId).Order());
        Assert.Equal(["jei", "velho"], r.ModsToDrop.Order());
        Assert.DoesNotContain("briga", r.ModsToDrop);
    }

    [Fact]
    public void Conflito_marcado_troca_o_mod_do_admin_pelo_do_autor()
    {
        var plano = UpstreamMerge.Plan(
            Base(("jei", "v1")),
            Deles(("jei", "v3")),
            Nossos(("jei", "v2")));

        var r = UpstreamMerge.Resolve(plano, Marcados(UpstreamItemKey.Mod("jei")));

        Assert.Contains("jei", r.ModsToDrop);
        var baixa = Assert.Single(r.ModsToIngest);
        Assert.Equal("v3", baixa.ToFileId);
    }

    [Fact]
    public void Atualizacao_desmarcada_mantem_o_jar_atual()
    {
        var plano = UpstreamMerge.Plan(
            Base(("jei", "v1")),
            Deles(("jei", "v2")),
            Nossos(("jei", "v1")));

        var r = UpstreamMerge.Resolve(plano, Marcados());

        Assert.True(r.IsEmpty);
    }

    [Fact]
    public void Config_em_conflito_marcado_recebe_o_do_autor_e_o_removido_sai()
    {
        var plano = UpstreamMerge.Plan(
            Base([], [("config/a.toml", "o"), ("config/b.toml", "o")]),
            Deles(),
            [Override("config/a.toml", "editado"), Override("config/b.toml", "editado")],
            new Dictionary<string, string> { ["config/a.toml"] = "autor" });

        var r = UpstreamMerge.Resolve(plano,
            Marcados(UpstreamItemKey.Override("config/a.toml"), UpstreamItemKey.Override("config/b.toml")));

        Assert.Equal("config/a.toml", Assert.Single(r.OverridesToWrite));
        Assert.Equal("config/b.toml", Assert.Single(r.OverridesToDrop));
    }

    [Fact]
    public void Item_que_o_admin_nao_viu_nao_entra()
    {
        // A escolha é do que ENTRA: se o autor publicou outra coisa entre a
        // simulação e o "Criar rascunho", o que não estava na tela fica de fora.
        var plano = UpstreamMerge.Plan(
            Base(("jei", "v1")),
            Deles(("jei", "v1"), ("surpresa", "s1")),
            Nossos(("jei", "v1")));

        var r = UpstreamMerge.Resolve(plano, Marcados(UpstreamItemKey.Mod("outro")));

        Assert.True(r.IsEmpty);
    }

    // ---- Fixtures ----

    private static UpstreamSelection Marcados(params string[] keys) =>
        new(keys.ToHashSet(StringComparer.OrdinalIgnoreCase));

    private static UpstreamSnapshot Base(params (string Project, string File)[] mods) => new()
    {
        Mods = mods.ToDictionary(m => m.Project, m => m.File), Overrides = new Dictionary<string, string>()
    };

    private static UpstreamSnapshot Base(
        (string Project, string File)[] mods, (string Path, string Sha)[] overrides) => new()
    {
        Mods = mods.ToDictionary(m => m.Project, m => m.File),
        Overrides = overrides.ToDictionary(o => o.Path, o => o.Sha)
    };

    private static ModpackFile Override(string path, string sha) => new()
    {
        ModpackVersionId = Guid.Empty,
        ProjectSlug = $"override:{path}",
        Path = path,
        Sha256 = sha,
        SizeBytes = 1,
        Side = FileSide.Both,
        Origin = ModFileOrigin.Override
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
