using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Quanta RAM uma instância entrega ao jogo.
///     É do JOGADOR, não do pack: a recomendação vem da versão, mas quem conhece
///     a máquina é quem está nela. Já havia campo no manifesto e um teste a
///     garantir que ele sobrevive às atualizações — faltava alguém conseguir
///     mudá-lo.
/// </summary>
public sealed class SetInstanceMemory(IInstanceStore instances)
{
    /// <summary>
    ///     Abaixo disto o servidor de Minecraft nem arranca, e o cliente com
    ///     mods muito menos. O mesmo piso que o painel aplica do outro lado.
    /// </summary>
    public const int MinimumMb = 512;

    /// <summary>
    ///     Grava a escolha. <paramref name="memoryMb" /> nulo volta a usar a
    ///     recomendada do pack, que é um estado legítimo e não uma limpeza.
    /// </summary>
    public async Task<MemoryResult> HandleAsync(
        InstalledInstance instance,
        int? memoryMb,
        CancellationToken ct)
    {
        if (memoryMb is { } escolhido && escolhido < MinimumMb)
            return MemoryResult.Failed($"O mínimo é {MinimumMb} MB.");

        // O manifesto inteiro é regravado com um campo trocado, e não um pedaço
        // dele: é o mesmo ficheiro que o diff da próxima atualização vai ler, e
        // escrever menos do que ele diz hoje faria o update seguinte achar que
        // os ficheiros que faltam na lista são lixo.
        await instances.WriteManifestAsync(
            instance.Key, instance.Manifest with { MemoryMb = memoryMb }, ct);

        return MemoryResult.Ok();
    }
}

public sealed record MemoryResult(bool Succeeded, string? Error)
{
    public static MemoryResult Ok() => new(true, null);

    public static MemoryResult Failed(string error) => new(false, error);
}
