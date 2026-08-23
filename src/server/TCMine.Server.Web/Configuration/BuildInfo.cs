using System.Reflection;

namespace TCMine.Server.Web.Configuration;

/// <summary>
///     A versão desta build, para a interface mostrar.
///     Vem do <c>InformationalVersion</c>, que o Dockerfile preenche a partir da
///     tag do git. Saber em que versão a instalação está deixou de ser detalhe
///     no dia em que uma imagem nova foi publicada e o container continuou
///     rodando a antiga — pelo painel não havia como perceber.
/// </summary>
public static class BuildInfo
{
    /// <summary>
    ///     Ex.: "0.1.5". Em build local sai "dev", porque aí o número não
    ///     significa nada: o padrão do SDK é 1.0.0 e mostrá-lo seria pior que
    ///     não mostrar versão alguma.
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
        var versao = bruta.Split('+')[0];

        return versao is "1.0.0" or "0.0.0" ? "dev" : versao;
    }
}
