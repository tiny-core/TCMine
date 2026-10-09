namespace TCMine.Launcher.Core.Runtime;

/// <summary>
///     Que Java uma versão do Minecraft exige, quando não há como perguntar.
///     PALPITE, e deliberadamente o último recurso: o primeiro é o
///     <see cref="TCMine.Launcher.Core.Abstractions.IJavaRequirementSource" />,
///     que lê o <c>javaVersion</c> declarado pela própria versão. Esta regra
///     existe para o caso de a versão ser desconhecida e não haver rede.
///     Já falhou uma vez, e vale registar como: o Minecraft trocou o esquema de
///     "1.y.z" para "ano.release" (25.x, 26.x), o parse daqui rejeitou "26.2",
///     caiu no fallback — e o fallback estava fixado numa constante que entretanto
///     tinha envelhecido. O jogo morria com "Could not create the Java Virtual
///     Machine" por causa de uma flag que o Java instalado não conhecia.
///     A lição não é "manter a constante atualizada": é que uma regra escrita à
///     mão sobre números de versão alheios envelhece sozinha, e por isso ela
///     deixou de ser a fonte primária.
/// </summary>
public static class JavaRequirement
{
    /// <summary>
    ///     O mais novo que conhecemos, e o que se assume no escuro.
    ///     Vai envelhecer outra vez — e está tudo bem, porque hoje é só o plano B.
    /// </summary>
    public const int Newest = 25;

    /// <summary>
    ///     Major do Java para uma versão do Minecraft ("1.20.4", "26.2").
    ///     O esquema novo ("ano.release", de 25.x em diante) é sempre o mais
    ///     recente que conhecemos — é posterior a tudo o que a tabela abaixo
    ///     cobre. Versão ilegível idem: assumir o antigo faria um pack atual
    ///     falhar de imediato, enquanto o contrário só afeta packs que já ninguém
    ///     publica.
    /// </summary>
    public static int ForMinecraft(string? minecraftVersion)
    {
        if (!TryParse(minecraftVersion, out var minor, out var patch))
            return Newest;

        return (minor, patch) switch
        {
            // 1.20.5 trouxe o salto para 21; 1.20.4 e abaixo ficaram no 17.
            (> 20, _) => 21,
            (20, >= 5) => 21,
            (>= 17, _) => 17,

            // 1.16.5 e abaixo: o Java 8 da época. Um pack assim é raro hoje, mas
            // rodá-lo em 21 falha — os loaders antigos usam APIs removidas.
            _ => 8
        };
    }

    /// <summary>
    ///     Lê o "y" e o "z" de "1.y.z", e só isso.
    ///     Qualquer outra coisa — o esquema "ano.release", uma snapshot, lixo —
    ///     não é lida de propósito: tudo o que não é "1.x" é posterior ao que a
    ///     tabela cobre, e mandar para o mais novo é a resposta certa para os
    ///     três casos.
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
