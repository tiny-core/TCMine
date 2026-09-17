namespace TCMine.Launcher.Core.Runtime;

/// <summary>
///     Que Java uma versão do Minecraft exige.
///     Regra pura e testada porque errar aqui não dá erro claro: o jogo abre e
///     morre no primeiro segundo com um <c>UnsupportedClassVersionError</c> no
///     log, que não diz "instale o Java 21" — diz um número de versão de
///     bytecode. É o tipo de falha que o jogador reporta como "não abre".
/// </summary>
public static class JavaRequirement
{
    /// <summary>O mais novo que usamos. Também o que se assume no escuro.</summary>
    public const int Newest = 21;

    /// <summary>
    ///     Major do Java para uma versão do Minecraft ("1.20.4", "1.21.1").
    ///     Versão que não se consegue ler devolve <see cref="Newest" />: o
    ///     catálogo de hoje é todo moderno, e assumir o antigo faria um pack
    ///     atual falhar de imediato, enquanto o contrário só afeta packs que já
    ///     ninguém publica.
    /// </summary>
    public static int ForMinecraft(string? minecraftVersion)
    {
        if (!TryParse(minecraftVersion, out var minor, out var patch))
            return Newest;

        return (minor, patch) switch
        {
            // 1.20.5 trouxe o salto para 21; 1.20.4 e abaixo ficaram no 17.
            ( > 20, _) => 21,
            (20, >= 5) => 21,
            ( >= 17, _) => 17,

            // 1.16.5 e abaixo: o Java 8 da época. Um pack assim é raro hoje, mas
            // rodá-lo em 21 falha — os loaders antigos usam APIs removidas.
            _ => 8
        };
    }

    /// <summary>
    ///     Lê o "y" e o "z" de "1.y.z". O "1." é constante desde sempre e o que
    ///     varia é o resto; um dia com "2.x" isto precisa mudar, e falhar a
    ///     leitura cai no mais novo, que é o comportamento certo nesse dia.
    /// </summary>
    private static bool TryParse(string? version, out int minor, out int patch)
    {
        minor = 0;
        patch = 0;

        if (string.IsNullOrWhiteSpace(version))
            return false;

        var partes = version.Trim().Split('.');

        if (partes.Length < 2 || partes[0] != "1" || !int.TryParse(partes[1], out minor))
            return false;

        // Sem patch é ".0": "1.21" e "1.21.0" são a mesma coisa. Um patch
        // ilegível ("1.20.4-pre1") também vale zero, e isso é conservador do
        // lado certo: 1.20.x cai no 17, que é o que um pré de 1.20.4 quer.
        if (partes.Length >= 3 && !int.TryParse(partes[2], out patch))
            patch = 0;

        return true;
    }
}
