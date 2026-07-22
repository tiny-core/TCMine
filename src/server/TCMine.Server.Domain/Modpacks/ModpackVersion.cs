using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Modpacks;

/// <summary>
///     Uma versão publicada é IMUTÁVEL.
///     Essa é a decisão central do modelo: depois que uma versão fica Ready, a
///     lista de arquivos nunca mais muda. É o que garante que um pack que
///     funcionava continue funcionando daqui a um ano, mesmo que um mod seja
///     despublicado na origem. Quer mudar alguma coisa? Cria a versão seguinte.
///     Ciclo de vida:
///     <code>
///     Draft ──> Resolving ──> Ready ──> Archived
///     │
///     └──────> Failed ──> (volta para Resolving ao tentar de novo)
///     </code>
/// </summary>
public sealed class ModpackVersion : Entity
{
    public required Guid ModpackId { get; set; }

    /// <summary>SemVer, ex: "1.4.0".</summary>
    public required string Version { get; set; }

    public required string MinecraftVersion { get; set; }
    public required ModLoader Loader { get; set; }
    public required string LoaderVersion { get; set; }

    // Os três abaixo têm setter privado: só mudam pelos métodos de transição.
    public ModpackVersionState State { get; private set; } = ModpackVersionState.Draft;
    public string? FailureReason { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public int? RecommendedMemoryMb { get; set; }

    public List<ModpackFile> Files { get; } = [];

    /// <summary>
    ///     Início da ingestão. Só faz sentido a partir de Draft ou de uma
    ///     tentativa que falhou.
    /// </summary>
    public void MarkResolving()
    {
        if (State is not (ModpackVersionState.Draft or ModpackVersionState.Failed))
            throw new InvalidOperationException(
                $"Não é possível iniciar a resolução a partir do estado {State}.");

        State = ModpackVersionState.Resolving;
        FailureReason = null;
        Touch();
    }

    /// <summary>
    ///     Publicação concluída. Daqui em diante a versão é imutável e pode ser
    ///     oferecida aos clientes.
    /// </summary>
    public void MarkReady()
    {
        if (State is not ModpackVersionState.Resolving)
            throw new InvalidOperationException(
                $"Não é possível publicar a partir do estado {State}.");

        if (Files.Count is 0)
            throw new InvalidOperationException("Não é possível publicar uma versão sem arquivos.");

        State = ModpackVersionState.Ready;
        PublishedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>
    ///     Falha na ingestão. O caso mais comum é um mod cujo autor marcou
    ///     allowModDistribution = false no CurseForge — não há solução técnica
    ///     legítima para isso, o admin precisa trocar o mod ou orientar o
    ///     jogador a baixar manualmente.
    /// </summary>
    public void MarkFailed(string reason)
    {
        State = ModpackVersionState.Failed;
        FailureReason = reason;
        Touch();
    }

    /// <summary>
    ///     Não oferecer a novos clientes, mas manter funcionando para quem já usa.
    ///     Nunca apague uma versão que tenha servidor apontando para ela.
    /// </summary>
    public void Archive()
    {
        if (State is not ModpackVersionState.Ready)
            throw new InvalidOperationException(
                $"Só é possível arquivar uma versão publicada. Estado atual: {State}.");

        State = ModpackVersionState.Archived;
        Touch();
    }
}