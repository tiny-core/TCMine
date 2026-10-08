using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Tests;

/// <summary>
///     A versão mostrada no menu.
///     Existe porque o rótulo só serve se for verdade: ele tem de ser o número
///     de <c>src/server/VERSION</c>, que é o que o workflow publica e transforma
///     em tag. Uma build que perdesse esse arquivo cairia no 1.0.0 padrão do SDK,
///     e o operador não teria como desconfiar.
/// </summary>
public sealed class BuildInfoTests
{
    [Fact]
    public void Nunca_devolve_vazio() => BuildInfo.Version.ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void Vem_do_arquivo_VERSION_do_servidor()
    {
        // Se este teste falhar mostrando "dev", o src/server/Directory.Build.props
        // deixou de alcançar o projeto Web — e toda imagem publicada passaria a
        // anunciar uma versão diferente da tag que o workflow cria.
        var expected = File.ReadAllText(FindServerVersionFile()).Trim();

        BuildInfo.Version.ShouldBe(expected);
    }

    [Fact]
    public void Nao_expoe_o_hash_do_commit() =>
        // O SDK acrescenta "+<sha>" ao InformationalVersion quando o repositório
        // é conhecido. Não cabe num rodapé e não diz nada a quem opera.
        BuildInfo.Version.ShouldNotContain("+");

    /// <summary>
    ///     Sobe da pasta do binário de teste até a raiz do repositório.
    /// </summary>
    private static string FindServerVersionFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "server", "VERSION");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("src/server/VERSION não encontrado a partir da pasta de testes.");
    }
}
