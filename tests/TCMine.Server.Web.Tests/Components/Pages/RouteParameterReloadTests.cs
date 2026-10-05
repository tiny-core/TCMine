using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Tests.Components.Pages;

/// <summary>
///     Página com id na rota tem de recarregar quando o id muda.
///     Navegar para a mesma página com outro id REAPROVEITA o componente: só os
///     parâmetros mudam, e o OnInitialized não roda de novo. Quem carregava só
///     ali ficava com os dados do id anterior — foi assim que o botão de
///     procurar mods sumia numa versão recém-criada até recarregar a página.
/// </summary>
public sealed class RouteParameterReloadTests
{
    [Fact]
    public void Pagina_com_id_na_rota_recarrega_em_OnParametersSet()
    {
        var pages = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ComponentBase).IsAssignableFrom(t) && t.GetCustomAttributes<RouteAttribute>().Any())
            .Where(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.PropertyType == typeof(Guid) && p.GetCustomAttribute<ParameterAttribute>() is not null))
            .ToArray();

        // A varredura tem de achar o caso que motivou o teste, senão passaria vazia.
        pages.Select(t => t.Name).ShouldContain("ModpackModsPage");

        var semRecarga = pages
            .Where(t => t.GetMethod("OnParametersSetAsync", BindingFlags.NonPublic | BindingFlags.Instance,
                            Type.EmptyTypes)?.DeclaringType == typeof(ComponentBase)
                        && t.GetMethod("OnParametersSet", BindingFlags.NonPublic | BindingFlags.Instance,
                            Type.EmptyTypes)?.DeclaringType == typeof(ComponentBase))
            .Select(t => t.Name)
            .Order()
            .ToArray();

        semRecarga.ShouldBeEmpty();
    }
}
