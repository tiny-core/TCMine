using System.Security.Cryptography;
using System.Text;

namespace TCMine.Launcher.Infrastructure;

/// <summary>
///     Resolve os diretórios do launcher.
///     <code>
///     Layout:
///     {raiz}/tcmine.json          ← sobrevive aos updates
///     {raiz}/store/               ← content store, compartilhado
///     {raiz}/instances/{hash}/    ← uma pasta por servidor pareado
///     {raiz}/runtimes/            ← JREs gerenciados por nós
///     </code>
///     A raiz é passada de fora porque quem sabe onde o App foi instalado é o
///     host: no Windows é %LOCALAPPDATA%\TCMine, e num futuro port seria outro
///     caminho.
/// </summary>
public sealed class LauncherPaths(string rootDirectory)
{
    public string RootDirectory { get; } = rootDirectory;

    public string StoreDirectory => Path.Combine(RootDirectory, "store");

    public string RuntimesDirectory => Path.Combine(RootDirectory, "runtimes");

    /// <summary>
    ///     Diretório de dados de um servidor específico.
    ///     Derivado de um hash curto da URL para que duas instalações apontando
    ///     para servidores diferentes convivam na mesma máquina. Usar o nome do
    ///     servidor daria problema com acento, barra e renomeação.
    /// </summary>
    public string InstanceRootFor(Uri serverUrl)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(serverUrl.ToString()));
        var curto = Convert.ToHexStringLower(hash)[..8];

        return Path.Combine(RootDirectory, "instances", curto);
    }
}