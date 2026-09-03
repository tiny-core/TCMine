namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Cria hardlinks, quando o sistema deixa.
///     Existe atrás de porta porque .NET não expõe hardlink: é P/Invoke no
///     Windows, e a chamada equivalente no Linux é outra. Sem a porta, o content
///     store — que é lógica portável — arrastaria API de sistema junto.
/// </summary>
public interface IFileLinker
{
    /// <summary>
    ///     Devolve <c>false</c> quando não deu, e isso é esperado: hardlink exige
    ///     o mesmo volume, e a pasta de instâncias pode estar noutro disco que o
    ///     store. Quem chama copia nesse caso, em vez de falhar.
    /// </summary>
    bool TryCreateHardLink(string existingPath, string newLinkPath);
}
