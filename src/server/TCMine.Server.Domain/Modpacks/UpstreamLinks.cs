namespace TCMine.Server.Domain.Modpacks;

/// <summary>
///     Link para a página de um projeto na origem.
///     Vive junto de <see cref="ModFileOrigin" /> porque duas partes precisam do
///     mesmo endereço: quem registra uma pendência (para o admin ir buscar o
///     arquivo à mão) e a grade de mods (para o selo de origem levar à página).
///     Duas cópias divergiriam no dia em que uma das origens mudasse de formato.
/// </summary>
public static class UpstreamLinks
{
    /// <summary>
    ///     Nulo quando não dá para montar um endereço confiável — e nesse caso é
    ///     melhor não oferecer link nenhum do que oferecer um que dá 404.
    ///     O CurseForge redireciona /projects/{id} para a página do mod; no
    ///     Modrinth o próprio id já é o caminho. Nenhuma das duas exige consulta.
    /// </summary>
    public static string? ProjectPage(ModFileOrigin origin, string? projectId)
    {
        if (projectId is not { Length: > 0 })
            return null;

        // Override e upload manual não vêm de projeto nenhum: o slug deles é
        // sintético e não corresponde a página alguma.
        return origin switch
        {
            ModFileOrigin.CurseForge => $"https://www.curseforge.com/projects/{projectId}",
            ModFileOrigin.Modrinth => $"https://modrinth.com/mod/{projectId}",
            _ => null
        };
    }
}
