namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Qual Java uma versão do Minecraft declara precisar.
///     Existe porque adivinhar pelo número da versão não sobrevive ao tempo: o
///     Minecraft trocou o esquema de "1.y.z" para "ano.release", e uma regra
///     escrita à mão passou a falhar em silêncio e a devolver o Java errado — o
///     jogo abria e morria com "Could not create the Java Virtual Machine".
///     O JSON da versão diz-no de forma autoritativa (<c>javaVersion</c>), e
///     dizê-lo é trabalho de quem sabe onde esse JSON vive.
/// </summary>
public interface IJavaRequirementSource
{
    /// <summary>
    ///     O major declarado, ou nulo quando não há como saber — versão
    ///     desconhecida, sem rede e sem cópia local. Nulo NÃO é erro: quem chama
    ///     recorre ao palpite, que continua a ser melhor do que não abrir.
    /// </summary>
    Task<int?> GetRequiredJavaAsync(string minecraftVersion, CancellationToken ct);
}
