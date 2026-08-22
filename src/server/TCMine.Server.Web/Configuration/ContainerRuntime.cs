namespace TCMine.Server.Web.Configuration;

/// <summary>
///     Estamos rodando dentro de um container?
///     Num lugar só porque duas validações de arranque dependem disso, e a
///     segunda foi escrita com um sinal errado: <c>/proc/self/mountinfo</c>
///     existe em QUALQUER Linux, não só em container, e usá-lo como pista fez a
///     verificação de coerência de volumes derrubar a aplicação no runner do CI
///     — que é Linux e não é container.
/// </summary>
public static class ContainerRuntime
{
    /// <summary>
    ///     A variável é marcada pelas imagens oficiais do .NET; o arquivo é o
    ///     sinal clássico e cobre imagem construída de outro jeito. Nenhum dos
    ///     dois aparece numa máquina Linux comum, que é o que importa aqui.
    /// </summary>
    public static bool IsContainer =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") is "true" or "1"
        || File.Exists("/.dockerenv");
}
