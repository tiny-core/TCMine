using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Windows;

/// <summary>
///     Hardlink NTFS.
///     É o que faz dez modpacks com o mesmo mod ocuparem um arquivo só no disco,
///     e criar uma instância custar milissegundos em vez de copiar centenas de
///     megabytes.
///     .NET não expõe hardlink — só link simbólico, que não serve: o simbólico
///     aponta para um caminho e quebraria se o store fosse limpo, além de o
///     Windows exigir privilégio para criá-lo fora do modo desenvolvedor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsFileLinker(ILogger<WindowsFileLinker> logger) : IFileLinker
{
    private readonly ILogger<WindowsFileLinker> _logger = logger;

    public bool TryCreateHardLink(string existingPath, string newLinkPath)
    {
        if (CreateHardLink(newLinkPath, existingPath, nint.Zero))
            return true;

        // Falhar é ESPERADO e não é erro: volume diferente entre o store e as
        // instâncias, sistema de arquivos sem suporte (FAT32, exFAT num disco
        // externo), ou o limite de 1023 links por arquivo. Quem chamou copia.
        //
        // O código do erro é lido ANTES da chamada de log (CA1873): qualquer
        // trabalho entre a API e o GetLastWin32Error pode sobrescrever o valor.
        var erro = Marshal.GetLastWin32Error();

        LogNaoLigou(erro, newLinkPath);

        return false;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW",
        StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateHardLink(string lpFileName, string lpExistingFileName, nint reserved);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Hardlink recusado (erro {Erro}) para {Destino}; será copiado.")]
    private partial void LogNaoLigou(int erro, string destino);
}
