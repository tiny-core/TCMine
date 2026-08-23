using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     "Aguardando upload manual" tem de significar exatamente isso.
///     A ingestão registra uma pendência <c>Queued</c> para CADA mod do pack
///     antes de começar, para que nenhum pedido se perca se o processo cair.
///     Enquanto ela corre, portanto, o pack inteiro está pendente — e a tela
///     chegou a anunciar centenas de mods "aguardando upload manual", que é o
///     oposto do que a frase quer dizer.
/// </summary>
public sealed class ManualUploadsTests
{
    [Fact]
    public void Enfileirado_nao_conta_como_upload_manual()
    {
        var version = Versao();
        version.UpsertPending(Pendencia(version.Id, "jei", PendingModReason.Queued));

        version.HasPendingMods.ShouldBeTrue("a pendência existe");
        version.HasManualUploads.ShouldBeFalse("mas não pede nada do admin");
        version.ManualUploads.ShouldBeEmpty();
    }

    [Fact]
    public void Motivo_real_conta_como_upload_manual()
    {
        var version = Versao();
        version.UpsertPending(Pendencia(version.Id, "jei", PendingModReason.Queued));
        version.UpsertPending(Pendencia(version.Id, "tombstone", PendingModReason.DistributionDenied));
        version.UpsertPending(Pendencia(version.Id, "overlays", PendingModReason.NoCompatibleFile));

        version.ManualUploads.Count.ShouldBe(2);
        version.ManualUploads.ShouldAllBe(p => p.Reason != PendingModReason.Queued);
    }

    [Fact]
    public void Enfileirado_que_falha_passa_a_contar()
    {
        // O caminho real: o mod é enfileirado e depois a origem recusa. A troca
        // acontece por slug, então é a MESMA pendência mudando de motivo.
        var version = Versao();
        version.UpsertPending(Pendencia(version.Id, "jei", PendingModReason.Queued));
        version.UpsertPending(Pendencia(version.Id, "jei", PendingModReason.DistributionDenied));

        version.PendingMods.Count.ShouldBe(1);
        version.ManualUploads.Count.ShouldBe(1);
    }

    private static ModpackVersion Versao() => new()
    {
        ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
    };

    private static PendingMod Pendencia(Guid versionId, string slug, PendingModReason reason) =>
        new()
        {
            ModpackVersionId = versionId,
            ProjectSlug = slug,
            DisplayName = slug,
            Origin = ModFileOrigin.CurseForge,
            Reason = reason
        };
}
