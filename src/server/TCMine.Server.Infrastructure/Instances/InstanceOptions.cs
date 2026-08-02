namespace TCMine.Server.Infrastructure.Instances;

public sealed class InstanceOptions
{
    /// <summary>Raiz das pastas de instância no host. Cada servidor tem {root}/{id}.</summary>
    public string RootPath { get; set; } = "/var/lib/tcmine/instances";
}
