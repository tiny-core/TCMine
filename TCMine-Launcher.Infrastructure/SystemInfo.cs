using System.Runtime.InteropServices;
using TCMine_Application.Launcher;

namespace TCMine_Launcher.Infrastructure;

/// <summary>Info do sistema (RAM física, para limitar o slider de memória). Implementa <see cref="ISystemInfo"/>.</summary>
public sealed class SystemInfo : ISystemInfo
{
    public int TotalPhysicalRamMb { get; } = DetectTotalRamMb();

    private static int DetectTotalRamMb()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var status = new MemoryStatusEx();
                if (GlobalMemoryStatusEx(status))
                    return (int)(status.ullTotalPhys / (1024UL * 1024UL));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (!line.StartsWith("MemTotal:", StringComparison.Ordinal)) continue;
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                        return (int)(kb / 1024);
                    break;
                }
            }
        }
        catch
        {
            // ignora — usa o fallback
        }

        return 16384;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In] [Out] MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private class MemoryStatusEx
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
