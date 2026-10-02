using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     No arranque do TCMine: dá a cada lease segurado um TTL inteiro a partir de
///     agora. Enquanto o TCMine esteve fora, nenhum servidor conseguiu mandar
///     heartbeat; sem isto os leases expirariam por culpa do TCMine e outro
///     servidor tomaria o canal de quem só não conseguiu avisar.
/// </summary>
public sealed class ExtendCloudLeasesAfterOutage(ICloudStorageRepository storage, TimeProvider clock)
{
    /// <returns>quantos leases foram estendidos</returns>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var extended = 0;
        foreach (var (lease, ttl) in await storage.ListHeldLeasesAsync(ct))
        {
            var read = lease.Version;
            lease.ExtendAfterOutage(now, ttl);
            // Conflito: um servidor já falou com este lease desde o arranque, e
            // o que ele gravou vale mais do que esta extensão.
            if (lease.Version != read && await storage.SaveLeaseAsync(lease, read, ct))
                extended++;
        }

        return extended;
    }
}
