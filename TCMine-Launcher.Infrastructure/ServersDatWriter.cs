using fNbt;
using TCMine_Domain.Launcher;

namespace TCMine_Launcher.Infrastructure;

/// <summary>
/// Escreve/atualiza o <c>servers.dat</c> (NBT) da instância — os servidores do modpack aparecem na lista
/// multijogador. Faz merge (não duplica). Colaborador interno do <see cref="GameLauncher"/>.
/// </summary>
internal static class ServersDatWriter
{
    public static void Ensure(string gameDir, IEnumerable<ModpackServer> servers)
    {
        var list = servers.ToList();
        if (list.Count == 0) return;

        Directory.CreateDirectory(gameDir);
        var path = Path.Combine(gameDir, "servers.dat");

        NbtCompound root;
        NbtList serverList;

        if (File.Exists(path))
        {
            var existing = new NbtFile();
            existing.LoadFromFile(path);
            root = existing.RootTag;
            var found = root.Get<NbtList>("servers");
            if (found is null)
            {
                found = new NbtList("servers", NbtTagType.Compound);
                root.Add(found);
            }

            serverList = found;
        }
        else
        {
            serverList = new NbtList("servers", NbtTagType.Compound);
            root = new NbtCompound("") { serverList };
        }

        var existingIps = serverList
            .OfType<NbtCompound>()
            .Select(c => c.Get<NbtString>("ip")?.Value)
            .Where(ip => ip is not null)
            .ToHashSet();

        foreach (var server in list)
        {
            if (existingIps.Contains(server.Ip)) continue;
            serverList.Add(new NbtCompound
            {
                new NbtString("name", server.Name),
                new NbtString("ip", server.Ip)
            });
        }

        new NbtFile(root).SaveToFile(path, NbtCompression.None);
    }
}
