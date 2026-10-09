using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     A instância que a tela de jogar mostra.
///     A escolha guardada é uma chave, não uma instância: a pasta pode ter sido
///     removida desde então, e resolver a chave contra o que está REALMENTE
///     instalado é a única coisa que impede a tela de jogar de oferecer um pack
///     fantasma — com botão e tudo — que já não existe no disco.
/// </summary>
public sealed class ChooseInstance(IInstanceStore instances, IActiveInstanceStore active)
{
    public Task<ActiveInstanceView> CurrentAsync(CancellationToken ct) =>
        CurrentAsync(null, ct);

    /// <summary>
    ///     Mesma coisa, mas reaproveitando uma listagem de instâncias que quem
    ///     chama já tem em mãos — evita listar a pasta de instâncias de novo
    ///     quando a tela de instâncias, que acabou de listá-las para desenhar a
    ///     própria página, também precisa saber qual está ativa.
    /// </summary>
    public async Task<ActiveInstanceView> CurrentAsync(
        IReadOnlyList<InstalledInstance>? knownInstances, CancellationToken ct)
    {
        var instaladas = knownInstances ?? await instances.ListAsync(ct);

        return new ActiveInstanceView(Resolve(instaladas, await active.ReadAsync(ct)), instaladas.Count);
    }

    public Task SetAsync(InstanceKey key, CancellationToken ct) => active.WriteAsync(key, ct);

    /// <summary>
    ///     Casa a escolha com o que existe. Função pura, e separada por isso: é a
    ///     única lógica deste caso de uso, e as três regras abaixo são o que a
    ///     tela de jogar tem de acertar.
    ///     Com uma instância só e nenhuma escolha, ela é a ativa. Pedir ao jogador
    ///     para "ativar" a única coisa que ele tem seria um clique para confirmar
    ///     o óbvio, logo depois de uma instalação que já demorou.
    ///     Com várias e nenhuma escolha, ninguém é ativa — e a tela pede para
    ///     escolher. Adivinhar (a mais recente, a maior) acertaria às vezes e
    ///     abriria o pack errado nas outras, o que é pior do que perguntar.
    /// </summary>
    public static InstalledInstance? Resolve(
        IReadOnlyList<InstalledInstance> installed,
        InstanceKey? chosen)
    {
        if (chosen is { } key && installed.FirstOrDefault(i => i.Key == key) is { } encontrada)
            return encontrada;

        return installed.Count is 1 ? installed[0] : null;
    }
}

/// <summary>
///     O que a tela de jogar precisa saber, numa varredura só.
///     A contagem vem junto porque sem ela a tela não distingue os dois vazios:
///     "instale alguma coisa" e "escolha entre as que já tem" são textos e
///     destinos diferentes, e perguntar duas vezes ao disco para descobrir isso
///     seria varrer a pasta de instâncias duas vezes por abertura.
/// </summary>
public sealed record ActiveInstanceView(InstalledInstance? Active, int InstalledCount)
{
    /// <summary>Há o que jogar, mas ninguém escolheu qual.</summary>
    public bool NeedsChoice => Active is null && InstalledCount > 0;
}
