using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Domain;

/// <summary>
///     A identidade de um arquivo enviado à mão. É o que deixa a versão nova de
///     um mod substituir a antiga, e o mod aparecer no inventário.
/// </summary>
public sealed class ManualModIdentityTests
{
    [Theory]
    [InlineData("mods/jei-1.2.jar", "JEI", "jar:jei")]
    [InlineData("mods/sem-id.jar", null, "file:sem-id.jar")]
    [InlineData("mods/Sem-Id.JAR", " ", "file:sem-id.jar")]
    public void Mod_ganha_identidade(string path, string? modId, string esperado) =>
        ModpackFile.ManualSlug(path, modId).ShouldBe(esperado);

    [Theory]
    [InlineData("config/jei.toml")]
    [InlineData("mods/config/x.jar")] // subpasta de mods não é carregada pelo loader
    [InlineData("resourcepacks/pack.zip")]
    public void Arquivo_que_nao_e_mod_fica_sem_identidade(string path) =>
        ModpackFile.ManualSlug(path, "qualquer").ShouldBeNull();
}
