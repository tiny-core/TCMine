using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Domain;

public sealed class ModpackVersionTests
{
    // ---- ReturnToDraft ----

    [Fact]
    public void ReturnToDraft_volta_de_Resolving_para_Draft()
    {
        var version = NewDraftVersion();
        version.MarkResolving();

        version.ReturnToDraft();

        Assert.Equal(ModpackVersionState.Draft, version.State);
    }

    [Fact]
    public void ReturnToDraft_limpa_a_razao_de_falha()
    {
        var version = NewDraftVersion();
        version.MarkResolving();

        version.ReturnToDraft();

        Assert.Null(version.FailureReason);
    }

    [Fact]
    public void ReturnToDraft_falha_se_nao_estiver_resolvendo()
    {
        // Draft não pode "voltar" para Draft — só de Resolving.
        var version = NewDraftVersion();

        Assert.Throws<InvalidOperationException>(() => version.ReturnToDraft());
    }

    // ---- UpsertFile ----

    [Fact]
    public void UpsertFile_adiciona_arquivo_novo_sem_substituir()
    {
        var version = NewDraftVersion();

        var replaced = version.UpsertFile(NewFile(version, "jei", "mods/jei-1.2.jar"));

        Assert.Null(replaced);
        Assert.Single(version.Files);
    }

    [Fact]
    public void UpsertFile_substitui_arquivo_do_mesmo_mod_e_devolve_o_id_antigo()
    {
        var version = NewDraftVersion();
        var jei12 = NewFile(version, "jei", "mods/jei-1.2.jar");
        version.UpsertFile(jei12);

        // Mesmo ProjectSlug, .jar diferente = atualização do mesmo mod.
        var jei15 = NewFile(version, "jei", "mods/jei-1.5.jar");
        var replaced = version.UpsertFile(jei15);

        Assert.Equal(jei12.Id, replaced); // devolveu o Id do antigo, para apagar a linha
        Assert.Single(version.Files); // não acumulou dois .jar do mesmo mod
        Assert.Equal("mods/jei-1.5.jar", version.Files.Single().Path);
    }

    [Fact]
    public void UpsertFile_mantem_mods_diferentes_lado_a_lado()
    {
        var version = NewDraftVersion();

        version.UpsertFile(NewFile(version, "jei", "mods/jei.jar"));
        var replaced = version.UpsertFile(NewFile(version, "create", "mods/create.jar"));

        Assert.Null(replaced);
        Assert.Equal(2, version.Files.Count);
    }

    [Fact]
    public void UpsertFile_sem_slug_apenas_adiciona()
    {
        // Upload manual solto (sem ProjectSlug): o domínio não deduplica por
        // slug; a unicidade por Path é responsabilidade do caso de uso.
        var version = NewDraftVersion();

        var r1 = version.UpsertFile(NewFile(version, null, "config/a.toml"));
        var r2 = version.UpsertFile(NewFile(version, null, "config/b.toml"));

        Assert.Null(r1);
        Assert.Null(r2);
        Assert.Equal(2, version.Files.Count);
    }

    [Fact]
    public void UpsertFile_falha_em_versao_publicada()
    {
        // Ready é imutável: nada de trocar arquivos.
        var version = NewReadyVersion();

        Assert.Throws<InvalidOperationException>(() =>
            version.UpsertFile(NewFile(version, "sodium", "mods/sodium.jar")));
    }

    // ---- Publicação (MarkReady) ----

    [Fact]
    public void MarkReady_publica_versao_com_arquivos()
    {
        var version = NewDraftVersion();
        version.UpsertFile(NewFile(version, "jei", "mods/jei.jar"));
        version.MarkResolving();

        version.MarkReady();

        Assert.Equal(ModpackVersionState.Ready, version.State);
    }

    [Fact]
    public void MarkReady_rejeita_versao_vazia()
    {
        // Publicar um pack sem mods não faz sentido — o launcher instalaria nada.
        var version = NewDraftVersion();
        version.MarkResolving();

        Assert.Throws<InvalidOperationException>(() => version.MarkReady());
    }

    // ---- Helpers ----

    private static ModpackVersion NewDraftVersion()
    {
        return new ModpackVersion { ModpackId = Guid.CreateVersion7(), Version = "1.0", LoaderVersion = "21.1.234" };
    }

    private static ModpackVersion NewReadyVersion()
    {
        var version = NewDraftVersion();
        version.UpsertFile(NewFile(version, "jei", "mods/jei.jar"));
        version.MarkResolving();
        version.MarkReady();
        return version;
    }

    private static ModpackFile NewFile(ModpackVersion version, string? projectSlug, string path)
    {
        return new ModpackFile
        {
            ModpackVersionId = version.Id,
            ProjectSlug = projectSlug,
            Path = path,
            // SHA-256 fake, só precisa de forma plausível (64 hex); o UpsertFile
            // não olha o hash, então o valor em si é irrelevante para estes testes.
            Sha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            SizeBytes = 1024,
            Side = FileSide.Both
        };
    }
}
