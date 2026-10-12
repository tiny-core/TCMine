using System.Reflection;

namespace TCMine.Server.Web.Configuration;

/// <summary>
///     A versão desta build, para a interface mostrar.
///     Vem do <c>InformationalVersion</c>, que o MSBuild preenche a partir de
///     <c>src/server/VERSION</c> (ver <c>src/server/Directory.Build.props</c>). Saber em que versão a instalação está
///     deixou de ser detalhe
///     no dia em que uma imagem nova foi publicada e o container continuou
///     rodando a antiga — pelo painel não havia como perceber.
/// </summary>
public static class BuildInfo
{
    /// <summary>
    ///     Ex.: "0.11.0". Sai "dev" quando o assembly não traz versão nenhuma.
    ///     O 1.0.0 padrão do SDK também virava "dev", para o caso de a build
    ///     perder o arquivo de versão; deixou de virar porque o
    ///     <c>Directory.Build.props</c> falha a build sem o arquivo, e o
    ///     <c>BuildInfoTests</c> confere que o número é o dele. Mantê-lo faria a
    ///     1.0.0 de verdade, quando sair, aparecer como "dev" e desligar o aviso
    ///     de atualização.
    /// </summary>
    public static string Version { get; } = Descobrir();

    private static string Descobrir()
    {
        var bruta = typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (bruta is not { Length: > 0 })
            return "dev";

        // O SDK acrescenta "+<sha do commit>" quando o repositório é conhecido.
        // O hash não cabe num rodapé e não diz nada a quem opera.
        var version = bruta.Split('+')[0];

        return version is "0.0.0" ? "dev" : version;
    }
}
