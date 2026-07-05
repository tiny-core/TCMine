using System.Runtime.InteropServices;

namespace TCMine_Server.Infrastructure.FileSystem;

/// <summary>
/// Estratégia padrão: liga arquivos por <b>hardlink</b> (custo de disco ~zero — o link e o cache
/// compartilham os mesmos bytes/inode) e pastas por recursão (hardlinkando cada arquivo interno).
///
/// <para><b>Por que não symlink em produção:</b> os servidores Minecraft rodam em containers-irmãos
/// (Docker-out-of-Docker) que montam só a pasta da instância. Um symlink apontaria para um caminho do
/// container do TCMine-Server (ex.: <c>/app/tcmine-data/server-cache/…</c>) que <b>não existe</b> no
/// container da instância → link quebrado. O hardlink vira um arquivo real na pasta da instância, então
/// resolve dentro do container. Exige mesma partição (o cache e a instância vivem sob <c>tcmine-data</c>,
/// então batem); se o hardlink falhar (partições diferentes/FS sem suporte), cai para cópia.</para>
/// </summary>
public sealed class CopyLinkStrategy : ILinkStrategy
{
    public string Name => "Hardlink/Copy";

    public void LinkFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination)) File.Delete(destination);

        // Hardlink primeiro (zero cópia de bytes). API nativa por SO; falha → cai para cópia.
        if (TryHardLink(source, destination)) return;

        File.Copy(source, destination, overwrite: true);
    }

    public void LinkDirectory(string source, string destination)
    {
        if (Directory.Exists(destination)) Directory.Delete(destination, true);
        CopyRecursive(new DirectoryInfo(source), destination);
    }

    // Copia uma árvore inteira, hardlinkando cada arquivo quando possível (economiza disco)
    private void CopyRecursive(DirectoryInfo dir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in dir.GetFiles())
            LinkFile(file.FullName, Path.Combine(targetDir, file.Name));

        foreach (var sub in dir.GetDirectories())
            CopyRecursive(sub, Path.Combine(targetDir, sub.Name));
    }

    private static bool TryHardLink(string source, string destination)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return CreateHardLink(destination, source, IntPtr.Zero);

            // Linux/macOS: link(2) do libc. 0 = sucesso.
            return LinkUnix(source, destination) == 0;
        }
        catch
        {
            return false; // qualquer falha → o chamador cai para File.Copy
        }
    }

    // CreateHardLink do Win32: cria um nome adicional para os mesmos dados (mesma partição NTFS).
    // Não exige privilégio de administrador, diferente dos symlinks no Windows.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    // link(2) do libc: cria um novo nome (hardlink) para um arquivo existente na mesma partição.
    // LPUTF8Str: paths no Linux são bytes UTF-8 — o marshaling explícito garante a codificação certa.
    // CA2101 suprimido de propósito: a regra foi desenhada para o par ANSI/Unicode do Win32 e quer
    // CharSet.Unicode (UTF-16), que estaria ERRADO para o libc no Linux. LPUTF8Str é o correto aqui.
#pragma warning disable CA2101
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int LinkUnix(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string oldpath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newpath);
#pragma warning restore CA2101
}
