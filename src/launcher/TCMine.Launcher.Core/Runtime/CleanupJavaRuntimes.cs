using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.Core.Runtime;

/// <summary>
///     Apaga os JREs que nenhuma instância instalada pede.
///     Eles acumulam sozinhos: cada versão do Minecraft exige um major diferente,
///     e um pack que salta de 21 para 25 deixa o 21 no disco para sempre —
///     duzentos megabytes que ninguém volta a abrir.
///     Explícito, e não automático no arranque. Apagar é barato de reverter (é
///     cache, volta a descarregar) mas não é barato de EXPLICAR: um launcher que
///     decide sozinho gastar banda no próximo clique é um launcher em que não se
///     confia. Com o número ao lado, quem decide é quem tem o disco.
/// </summary>
public sealed class CleanupJavaRuntimes(
    IInstanceStore instances,
    IJavaRequirementSource requirements,
    IJavaLocator java,
    GameSession session)
{
    /// <summary>O que dá para libertar agora, sem apagar nada.</summary>
    public async Task<IReadOnlyList<InstalledRuntime>> FindUnusedAsync(CancellationToken ct)
    {
        var installed = await java.ListAsync(ct);

        if (installed.Count is 0)
            return [];

        var needed = await RequiredMajorsAsync(ct);

        return [.. installed.Where(r => !needed.Contains(r.MajorVersion))];
    }

    /// <summary>
    ///     Apaga os que sobram e devolve quanto libertou.
    ///     Recusa-se com o jogo aberto: o processo em execução está a correr a
    ///     partir de uma destas pastas, e puxá-la debaixo dele mata a partida.
    /// </summary>
    public async Task<long> HandleAsync(CancellationToken ct)
    {
        if (session.IsRunning)
            return 0;

        long freed = 0;

        foreach (var runtime in await FindUnusedAsync(ct))
        {
            await java.RemoveAsync(runtime.MajorVersion, ct);
            freed += runtime.SizeBytes;
        }

        return freed;
    }

    /// <summary>
    ///     Os majors que as instâncias pedem hoje.
    ///     Usa a MESMA resolução do arranque do jogo — versão primeiro, palpite
    ///     depois —, e é por isso que o conjunto bate: apagar algo que o launch
    ///     iria pedir custaria uma descarga, não uma falha. Sem rede o palpite
    ///     pode divergir do que está instalado; o preço disso é banda, e é o
    ///     preço certo a pagar por não ter de adivinhar melhor.
    /// </summary>
    private async Task<HashSet<int>> RequiredMajorsAsync(CancellationToken ct)
    {
        var needed = new HashSet<int>();

        foreach (var instance in await instances.ListAsync(ct))
        {
            var minecraft = instance.Manifest.MinecraftVersion;

            needed.Add(await requirements.GetRequiredJavaAsync(minecraft ?? "", ct)
                       ?? JavaRequirement.ForMinecraft(minecraft));
        }

        return needed;
    }
}
