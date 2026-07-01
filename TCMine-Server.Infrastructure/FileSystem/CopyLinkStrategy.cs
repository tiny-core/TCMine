using System.Runtime.InteropServices;

namespace TCMine_Server.Infrastructure.FileSystem;

/// <summary>
/// Estratégia de dev (Windows, sem admin/Developer Mode, onde symlinks falham):
///
/// <list type="bullet">
/// <item><b>Arquivo</b> → tenta <b>hardlink</b> (sem privilégio, mesma partição; o jar do cache e o
/// link compartilham os mesmos bytes em disco). Se o hardlink falhar (ex.: partições diferentes),
/// cai para cópia.</item>
/// <item><b>Pasta</b> → cópia recursiva (hardlinkando cada arquivo interno quando dá). É o
/// trade-off aceitável em dev: mais disco que symlink, mas funciona sem privilégio elevado.</item>
/// </list>
///
/// Em produção usa-se a <see cref="SymlinkStrategy"/>; esta existe para o ambiente de dev no Windows.
/// </summary>
public sealed class CopyLinkStrategy : ILinkStrategy
{
    public string Name => "Copy/Hardlink";

    public void LinkFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination)) File.Delete(destination);

        // Tenta hardlink primeiro (zero cópia de bytes); só Windows tem a API nativa aqui
        if (OperatingSystem.IsWindows() && TryHardLinkWindows(source, destination)) return;

        File.Copy(source, destination, overwrite: true);
    }

    public void LinkDirectory(string source, string destination)
    {
        if (Directory.Exists(destination)) Directory.Delete(destination, true);
        CopyRecursive(new DirectoryInfo(source), destination);
    }

    // Copia uma árvore inteira, hardlinkando cada arquivo quando possível (economiza disco em dev)
    private void CopyRecursive(DirectoryInfo dir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in dir.GetFiles())
            LinkFile(file.FullName, Path.Combine(targetDir, file.Name));

        foreach (var sub in dir.GetDirectories())
            CopyRecursive(sub, Path.Combine(targetDir, sub.Name));
    }

    // CreateHardLink do Win32: cria um nome adicional para os mesmos dados (mesma partição NTFS).
    // Não exige privilégio de administrador, diferente dos symlinks no Windows.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    private static bool TryHardLinkWindows(string source, string destination)
    {
        try
        {
            return CreateHardLink(destination, source, IntPtr.Zero);
        }
        catch
        {
            return false; // qualquer falha → o chamador cai para File.Copy
        }
    }
}
