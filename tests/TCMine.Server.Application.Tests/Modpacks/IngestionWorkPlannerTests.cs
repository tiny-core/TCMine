using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     O cálculo que torna a fila de ingestão recuperável sem tabela de jobs.
///     Ele responde "o que ainda falta?" a partir do estado gravado, e é usado
///     tanto pelo reparo que o admin pede quanto pela recuperação automática do
///     arranque. Errar para menos deixa a versão eternamente incompleta; errar
///     para mais refaz download já feito e insiste em mod que o autor proibiu.
/// </summary>
public sealed class IngestionWorkPlannerTests
{
    [Fact]
    public void Pendencia_comum_entra_no_plano()
    {
        var (modpack, version) = Montar();
        version.UpsertPending(Pendente("jei", PendingModReason.Queued));

        var plano = IngestionWorkPlanner.PlanRetry(version, modpack);

        plano.ShouldHaveSingleItem().ProjectId.ShouldBe("jei");
    }

    [Fact]
    public void Mod_com_redistribuicao_negada_fica_de_fora()
    {
        // É decisão do autor do mod, não falha nossa. Insistir gasta chamada de
        // API e devolve o mesmo "não" toda vez.
        var (modpack, version) = Montar();
        version.UpsertPending(Pendente("proibido", PendingModReason.DistributionDenied));

        IngestionWorkPlanner.PlanRetry(version, modpack).ShouldBeEmpty();
    }

    [Fact]
    public void Snapshot_recupera_o_que_sumiu_sem_virar_pendencia()
    {
        // Ingestão interrompida no meio: o mod não chegou a virar arquivo nem
        // pendência. Sem o snapshot ele ficaria invisível para sempre.
        var (modpack, version) = ComSnapshot("jei", "sodium");

        var plano = IngestionWorkPlanner.PlanRetry(version, modpack);

        plano.Select(i => i.ProjectId).OrderBy(s => s).ShouldBe(["jei", "sodium"]);
    }

    [Fact]
    public void Mod_ja_baixado_nao_volta_para_o_plano()
    {
        var (modpack, version) = ComSnapshot("jei", "sodium");

        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = "mods/jei.jar",
            Sha256 = new string('a', 64),
            SizeBytes = 1,
            Side = FileSide.Both,
            Origin = ModFileOrigin.CurseForge,
            ProjectSlug = "jei"
        });

        IngestionWorkPlanner.PlanRetry(version, modpack)
            .ShouldHaveSingleItem().ProjectId.ShouldBe("sodium");
    }

    [Fact]
    public void Pendencia_manda_mais_que_o_snapshot_para_o_mesmo_mod()
    {
        // A pendência carrega o FileId que de fato foi pedido; o snapshot traz o
        // que o pack de origem prometia. Duplicar o mod no plano faria o mesmo
        // download duas vezes.
        var (modpack, version) = ComSnapshot("jei");
        version.UpsertPending(Pendente("jei", PendingModReason.Queued, fileId: "999"));

        var item = IngestionWorkPlanner.PlanRetry(version, modpack).ShouldHaveSingleItem();
        item.FileId.ShouldBe("999");
    }

    [Fact]
    public void Versao_sem_pendencia_nem_snapshot_nao_tem_o_que_fazer()
    {
        var (modpack, version) = Montar();

        IngestionWorkPlanner.PlanRetry(version, modpack).ShouldBeEmpty();
    }

    private static PendingMod Pendente(
        string slug, PendingModReason reason, string? fileId = null, Guid versionId = default) => new()
    {
        ModpackVersionId = versionId,
        DisplayName = slug,
        ProjectSlug = slug,
        Origin = ModFileOrigin.CurseForge,
        Reason = reason,
        FileId = fileId,
        Side = FileSide.Both
    };

    private static (Modpack Modpack, ModpackVersion Version) Montar()
    {
        var modpack = new Modpack
        {
            Slug = "teste", Name = "Teste", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
        };

        var version = new ModpackVersion
        {
            ModpackId = modpack.Id, Version = "1.0.0", LoaderVersion = "21.1.100"
        };

        return (modpack, version);
    }

    private static (Modpack Modpack, ModpackVersion Version) ComSnapshot(params string[] mods)
    {
        var (modpack, version) = Montar();

        modpack.UpstreamProvider = ModFileOrigin.CurseForge;
        version.UpstreamSnapshotJson = new UpstreamSnapshot
        {
            Mods = mods.ToDictionary(m => m, _ => "1"),
            Overrides = new Dictionary<string, string>()
        }.ToJson();

        return (modpack, version);
    }
}
