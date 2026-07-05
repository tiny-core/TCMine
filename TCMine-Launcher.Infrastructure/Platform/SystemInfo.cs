using System.Globalization;
using System.Runtime.InteropServices;
using TCMine_Application.Launcher;

namespace TCMine_Launcher.Infrastructure.Platform;

/// <summary>Info do sistema (RAM física, para limitar o slider de memória). Implementa <see cref="ISystemInfo" />.</summary>
public sealed class SystemInfo : ISystemInfo
{
    public int TotalPhysicalRamMb { get; } = DetectTotalRamMb();

    private static int DetectTotalRamMb()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var status = new MemoryStatusEx
                {
                    dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>()
                };

                if (GlobalMemoryStatusEx(ref status))
                    return ToSafeIntMb(status.ullTotalPhys / (1024UL * 1024UL));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (!line.StartsWith("MemTotal:", StringComparison.Ordinal))
                        continue;

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 2 &&
                        long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var kb))
                        return kb / 1024 > int.MaxValue ? int.MaxValue : (int)(kb / 1024);

                    break;
                }
            }
        }
        catch
        {
            // Use fallback.
        }

        return 16384;
    }

    private static int ToSafeIntMb(ulong mb)
    {
        return mb > int.MaxValue ? int.MaxValue : (int)mb;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", SetLastError = true)]
#pragma warning disable SYSLIB1054
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);
#pragma warning restore SYSLIB1054

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
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