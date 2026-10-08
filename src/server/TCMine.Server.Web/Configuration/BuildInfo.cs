using System.Reflection;

namespace TCMine.Server.Web.Configuration;

/// <summary>
///     A versão desta build, para a interface mostrar.
///     Vem do <c>InformationalVersion</c>, que o MSBuild preenche a partir de
///     <c>src/server/VERSION</c> (ver <c>src/server/Directory.Build.props</c>). Saber em que versão a instalação está deixou de ser detalhe
///     no dia em que uma imagem nova foi publicada e o container continuou
///     rodando a antiga — pelo painel não havia como perceber.
/// </summary>
public static class BuildInfo
{
    /// <summary>
    ///     Ex.: "1.1.0". Só sai "dev" se a build perdeu o arquivo de versão: aí
    ///     o SDK cai no padrão 1.0.0, e mostrá-lo seria pior que não mostrar
    ///     versão alguma.
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

        return version is "1.0.0" or "0.0.0" ? "dev" : version;
    }
}
