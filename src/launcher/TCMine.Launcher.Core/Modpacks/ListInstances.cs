using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;

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

    /// <summary>
    ///     Só os manifestos, sem o tamanho de nenhuma pasta. Para telas que só
    ///     precisam saber QUAL modpack/versão já está instalado (ex.: a tela de
    ///     modpacks, para desligar "instalar de novo") — pedir o tamanho de
    ///     mundos inteiros para responder isso é trabalho que ninguém usa.
    /// </summary>
    public Task<IReadOnlyList<InstanceManifest>> HandleLightAsync(CancellationToken ct) =>
        instances.ListManifestsAsync(ct);

    public Task RemoveAsync(InstalledInstance instance, CancellationToken ct) =>
        instances.RemoveAsync(instance.Key, ct);
}
