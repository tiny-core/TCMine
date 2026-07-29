using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

public class ModpackVersionTests
{
    private static ModpackVersion NovaVersao()
    {
        return new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(),
            Version = "1.0.0",
            LoaderVersion = "21.1.0"
        };
    }

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
    public void Versao_nova_comeca_em_draft()
    {
        NovaVersao().State.ShouldBe(ModpackVersionState.Draft);
    }

    [Fact]
    public void Nao_publica_pulando_a_resolucao()
    {
        var versao = NovaVersao();

        Should.Throw<InvalidOperationException>(() => versao.MarkReady());
    }

    [Fact]
    public void Nao_publica_versao_sem_arquivos()
    {
        // Um pack vazio passaria batido e só quebraria na máquina do jogador.
        var versao = NovaVersao();
        versao.MarkResolving();

        Should.Throw<InvalidOperationException>(() => versao.MarkReady());
    }

    [Fact]
    public void Fluxo_feliz_leva_a_ready_com_data_de_publicacao()
    {
        var versao = NovaVersao();
        versao.MarkResolving();
        versao.Files.Add(ArquivoQualquer(versao.Id));

        versao.MarkReady();

        versao.State.ShouldBe(ModpackVersionState.Ready);
        versao.PublishedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Versao_que_falhou_pode_tentar_de_novo()
    {
        var versao = NovaVersao();
        versao.MarkResolving();
        versao.MarkFailed("Mod X não permite redistribuição.");

        versao.MarkResolving();

        versao.State.ShouldBe(ModpackVersionState.Resolving);
        versao.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void Nao_arquiva_versao_que_nunca_foi_publicada()
    {
        var versao = NovaVersao();

        Should.Throw<InvalidOperationException>(() => versao.Archive());
    }
}