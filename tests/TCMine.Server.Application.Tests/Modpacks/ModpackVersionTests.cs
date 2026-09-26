using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

public class ModpackVersionTests
{
    private static ModpackVersion NovaVersao() =>
        new() { ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.0" };

    private static ModpackFile ArquivoQualquer(Guid versaoId)
    {
        return new ModpackFile
        {
            ModpackVersionId = versaoId,
            Path = "mods/jei.jar",
            Sha256 = new string('a', 64),
            SizeBytes = 1024,
            Side = FileSide.Both
        };
    }

    [Fact]
    public void Versao_nova_comeca_em_draft() => NovaVersao().State.ShouldBe(ModpackVersionState.Draft);

    [Fact]
    public void Nao_publica_pulando_a_resolucao()
    {
        var version = NovaVersao();

        Should.Throw<InvalidOperationException>(() => version.MarkReady());
    }

    [Fact]
    public void Nao_publica_versao_sem_arquivos()
    {
        // Um pack vazio passaria batido e só quebraria na máquina do jogador.
        var version = NovaVersao();
        version.MarkResolving();

        Should.Throw<InvalidOperationException>(() => version.MarkReady());
    }

    [Fact]
    public void Fluxo_feliz_leva_a_ready_com_data_de_publicacao()
    {
        var version = NovaVersao();
        version.MarkResolving();
        version.UpsertFile(ArquivoQualquer(version.Id));

        version.MarkReady();

        version.State.ShouldBe(ModpackVersionState.Ready);
        version.PublishedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Versao_que_falhou_pode_tentar_de_novo()
    {
        var version = NovaVersao();
        version.MarkResolving();
        version.MarkFailed("Mod X não permite redistribuição.");

        version.MarkResolving();

        version.State.ShouldBe(ModpackVersionState.Resolving);
        version.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void Nao_arquiva_versao_que_nunca_foi_publicada()
    {
        var version = NovaVersao();

        Should.Throw<InvalidOperationException>(() => version.Archive());
    }
}
