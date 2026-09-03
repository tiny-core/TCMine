namespace TCMine.Launcher.UI.Layout;

/// <summary>
///     Decide qual item do trilho de navegação fica aceso.
///     Está fora do componente porque é a única lógica de verdade do trilho, e
///     dentro de um .razor ela ficaria fora do alcance de qualquer teste.
/// </summary>
public static class NavMatch
{
    /// <param name="href">Rota do item, como declarada no trilho.</param>
    /// <param name="relativePath">
    ///     Caminho atual relativo à base — o que
    ///     <c>NavigationManager.ToBaseRelativePath</c> devolve.
    /// </param>
    public static bool IsActive(string href, string relativePath)
    {
        var alvo = Normalize(href);
        var atual = Normalize(relativePath);

        // A raiz casa exato. Por prefixo ela casaria com tudo, e "Jogar" ficaria
        // aceso em todas as telas.
        if (alvo is "/")
            return atual is "/";

        if (!atual.StartsWith(alvo, StringComparison.OrdinalIgnoreCase))
            return false;

        // Prefixo só vale terminando em fronteira de segmento: é o que mantém o
        // item do pai aceso numa rota filha (/modpacks/{id}) sem acender
        // "/mods" numa rota "/modspack" que só compartilha as letras.
        return atual.Length == alvo.Length || atual[alvo.Length] is '/';
    }

    /// <summary>
    ///     Reduz à forma comparável: sem query, sem fragmento, com uma barra à
    ///     frente e nenhuma atrás. Sem isto, "/modpacks" e "modpacks/" seriam
    ///     rotas diferentes conforme o link que trouxe o jogador até aqui.
    /// </summary>
    private static string Normalize(string caminho)
    {
        var limpo = caminho.Split('?')[0].Split('#')[0].Trim('/');

        return limpo.Length is 0 ? "/" : "/" + limpo;
    }
}
