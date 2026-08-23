using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Quem somos para o sistema de arquivos.
///     Existe por causa de uma falha que só aparece em produção: o TCMine
///     materializa a pasta da instância com o usuário do PRÓPRIO container
///     (1654, definido no Dockerfile), e o container do jogo roda como 1000 por
///     padrão na imagem itzg. Resultado: o servidor de jogo não conseguia
///     escrever no próprio /data e morria com "permission denied", num diretório
///     que estava lá e com dono aparentemente correto.
///     Passar estes números ao container do jogo faz os dois lados usarem o
///     mesmo usuário. Ler em vez de fixar 1654 porque quem instala pode rodar o
///     TCMine como outro usuário, e aí o valor fixo recriaria o problema ao
///     contrário.
/// </summary>
internal static partial class ProcessUser
{
    /// <summary>UID e GID efetivos, ou nulo fora do Linux (onde não se aplica).</summary>
    public static (int Uid, int Gid)? Current => OperatingSystem.IsLinux() ? (GetUid(), GetGid()) : null;

    [SupportedOSPlatform("linux")]
    [LibraryImport("libc", EntryPoint = "geteuid")]
    private static partial int GetUidCore();

    [SupportedOSPlatform("linux")]
    [LibraryImport("libc", EntryPoint = "getegid")]
    private static partial int GetGidCore();

    [SupportedOSPlatform("linux")]
    private static int GetUid() => GetUidCore();

    [SupportedOSPlatform("linux")]
    private static int GetGid() => GetGidCore();
}
