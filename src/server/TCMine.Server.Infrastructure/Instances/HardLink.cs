using System.Runtime.InteropServices;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Hardlink de arquivo, com fallback silencioso. link() no Unix, CreateHardLink
///     no Windows. Falha esperada quando origem e destino estão em volumes
///     diferentes (EXDEV) — aí o chamador copia.
/// </summary>
internal static partial class HardLink
{
    public static bool TryCreate(string source, string link)
    {
        try
        {
            return OperatingSystem.IsWindows()
                ? CreateHardLinkW(link, source, IntPtr.Zero)
                : LinkUnix(source, link) == 0;
        }
        catch
        {
            return false;
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateHardLinkW(string lpFileName, string lpExistingFileName,
        IntPtr lpSecurityAttributes);

    [LibraryImport("libc", EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int LinkUnix(string oldpath, string newpath);
}