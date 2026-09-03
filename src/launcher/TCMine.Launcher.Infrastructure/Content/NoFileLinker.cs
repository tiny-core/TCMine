using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Content;

/// <summary>
///     O linker que nunca liga.
///     Registrado por padrão para que a infraestrutura portável funcione sozinha:
///     sem hardlink o store copia, que é mais disco e nada mais. O host de
///     Windows substitui por <c>WindowsFileLinker</c>.
///     Existe em vez de um <c>IFileLinker?</c> opcional porque uma dependência
///     nula obrigaria cada uso a lembrar do caso; aqui o caso não existe.
/// </summary>
public sealed class NoFileLinker : IFileLinker
{
    public bool TryCreateHardLink(string existingPath, string newLinkPath) => false;
}
