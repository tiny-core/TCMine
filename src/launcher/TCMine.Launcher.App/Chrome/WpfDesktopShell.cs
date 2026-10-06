using System.Diagnostics;
using System.IO;
using TCMine.Launcher.UI.Abstractions;

namespace TCMine.Launcher.App.Chrome;

/// <summary>
///     Abre pastas no explorador do Windows.
/// </summary>
internal sealed class WpfDesktopShell : IDesktopShell
{
    public void OpenFolder(string path)
    {
        // Pasta que não existe abriria o explorador em "Documentos", o que
        // pareceria um bug. Silenciar é melhor: a tela só oferece o botão para
        // instâncias que ela acabou de listar do disco.
        if (!Directory.Exists(path))
            return;

        // UseShellExecute: sem isto o Process.Start tenta EXECUTAR o caminho como
        // um programa, e uma pasta não é executável.
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    public void OpenUrl(string url)
    {
        // Só http(s): um link na tela Sobre é sempre nosso, mas esta porta é
        // genérica, e UseShellExecute com uma string arbitrária executaria
        // qualquer coisa que o shell soubesse abrir — não só páginas.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            return;

        Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
    }
}
