using System.Reflection;

namespace TCMine.Server.Web;

/// <summary>
///     Versão desta build, lida do assembly.
///     Informativa apenas: nenhuma decisão de compatibilidade usa este valor.
///     Quem decide isso é o Protocol.
/// </summary>
internal static class ThisAssembly
{
    public static string Version { get; } =
        typeof(ThisAssembly).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0] // remove o hash do commit que o SDK anexa
        ?? "0.0.0";
}
