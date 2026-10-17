using System.Reflection;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Architecture.Tests;

/// <summary>
///     Toda escrita num repositório que o launcher lê tem de dizer se o avisa.
///     O aviso mora no repositório (ver <see cref="IResourceChanges" />), e um
///     método novo que o esquecesse deixaria os launchers com dados velhos até
///     alguém navegar — sem nenhum teste de comportamento reparar. Esta regra
///     não confere que o aviso é ENVIADO; obriga quem escreve o método a
///     decidir, com <see cref="InvalidatesAttribute" /> ou
///     <see cref="NotVisibleToLauncherAttribute" />.
/// </summary>
public class LauncherInvalidationRules
{
    /// <summary>
    ///     Os repositórios de onde vem o que o hub entrega ao launcher. Um
    ///     repositório novo nessa condição entra aqui.
    ///     De fora, e de propósito: o de membros de MODPACK (o catálogo não é
    ///     filtrado por eles) e o de usuários (a fusão de contas re-aponta
    ///     acessos, mas é rara e o launcher rebusca tudo ao religar).
    /// </summary>
    private static readonly Type[] RepositoriesTheLauncherReads =
    [
        typeof(IModpackRepository),
        typeof(IServerRepository),
        typeof(INewsRepository),
        typeof(IInviteRepository),
        typeof(IMembershipRepository),
        typeof(IAccessRequestRepository),
        typeof(ISettingsRepository),
        typeof(IPlayerSettingsRepository)
    ];

    [Fact]
    public void Toda_escrita_dos_repositorios_que_o_launcher_le_decide_se_o_avisa()
    {
        var semDecisao = RepositoriesTheLauncherReads
            .SelectMany(type => type.GetMethods().Select(method => (Type: type, Method: method)))
            .Where(x => IsWrite(x.Method))
            .Where(x => x.Method.GetCustomAttribute<InvalidatesAttribute>() is null
                        && x.Method.GetCustomAttribute<NotVisibleToLauncherAttribute>() is null)
            .Select(x => $"{x.Type.Name}.{x.Method.Name}")
            .ToArray();

        semDecisao.ShouldBeEmpty(
            "marque com [Invalidates] (e chame IResourceChanges.Touch na implementação) "
            + "ou com [NotVisibleToLauncher(\"motivo\")]");
    }

    /// <summary>Leitura pelo nome, que é a convenção dos repositórios daqui.</summary>
    private static bool IsWrite(MethodInfo method) =>
        !method.Name.StartsWith("Get", StringComparison.Ordinal)
        && !method.Name.StartsWith("List", StringComparison.Ordinal)
        && !method.Name.StartsWith("Find", StringComparison.Ordinal)
        && !method.Name.Contains("Exists", StringComparison.Ordinal);
}
