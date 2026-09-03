using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     O que está instalado nesta máquina.
///     Lê do disco e não do servidor: a tela de instâncias precisa funcionar sem
///     rede, porque desinstalar para liberar espaço é justamente o que se faz
///     quando nada mais está funcionando.
/// </summary>
public sealed class ListInstances(IInstanceStore instances)
{
    public Task<IReadOnlyList<InstalledInstance>> HandleAsync(CancellationToken ct) =>
        instances.ListAsync(ct);

    public Task RemoveAsync(InstalledInstance instance, CancellationToken ct) =>
        instances.RemoveAsync(instance.Key, ct);
}
