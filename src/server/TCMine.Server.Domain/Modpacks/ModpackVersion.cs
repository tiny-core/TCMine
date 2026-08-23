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

    /// <summary>
    ///     Versão pré-release (tem sufixo, ex: "-alpha"). SemVer: qualquer coisa
    ///     depois do hífen é pré-release. Servidores só rodam releases estáveis.
    /// </summary>
    public bool IsPreRelease => Version.Contains('-');

    /// <summary>SemVer, ex: "1.4.0".</summary>
    public required string Version { get; set; }

    public required string LoaderVersion { get; set; }

    // Os três abaixo têm setter privado: só mudam pelos métodos de transição.
    public ModpackVersionState State { get; private set; } = ModpackVersionState.Draft;
    public string? FailureReason { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public int? RecommendedMemoryMb { get; set; }

    // ---------- Origem externa (versão importada de um pack) ----------

    /// <summary>
    ///     Release do pack de origem que esta versão espelha (fileId no
    ///     CurseForge). É comparando isto com a release mais recente lá fora que
    ///     sabemos se há atualização — e é o que mantém a versão do CurseForge
    ///     separada da nossa numeração.
    /// </summary>
    public string? UpstreamFileId { get; set; }

    /// <summary>Rótulo da versão na origem, ex.: "1.20.1-4.2". Só para exibir.</summary>
    public string? UpstreamVersionLabel { get; set; }

    /// <summary>
    ///     Retrato do pack como veio da origem (JSON): a lista de mods e os
    ///     overrides com seus hashes. É a BASE do merge de três vias — comparando
    ///     base × origem nova × estado atual dá para saber o que o autor mudou, o
    ///     que o admin mudou, e onde os dois se chocam.
    /// </summary>
    public string? UpstreamSnapshotJson { get; set; }

    /// <summary>
    ///     Página do server pack publicado pelo autor na origem, quando existe.
    ///     O TCMine monta a instância a partir desta versão, então o zip do autor
    ///     não entra no fluxo — mas ele é a referência de quais mods o autor
    ///     considera de servidor, e vale ser dito a quem vai criar um.
    /// </summary>
    public string? UpstreamServerPackUrl { get; set; }

    public List<ModpackFile> Files { get; } = [];

    /// <summary>
    ///     Mods que a ingestão não trouxe e que esperam upload manual. Não
    ///     impedem a versão de existir — impedem publicar sem o admin assumir.
    /// </summary>
    public List<PendingMod> PendingMods { get; } = [];

    public bool HasPendingMods => PendingMods.Count > 0;

    /// <summary>
    ///     As pendências que de fato pedem ação do admin.
    ///     Uma pendência <c>Queued</c> não pede nada: ela só registra que o mod
    ///     foi enfileirado, para que um pedido não se perca se o processo cair
    ///     antes do worker chegar nele. Enquanto a resolução corre, TODOS os
    ///     mods do pack estão nesse estado — quatrocentos e oitenta, no caso de
    ///     um pack grande —, e contá-los como "aguardando upload manual" é
    ///     dizer ao admin que ele tem centenas de arquivos para subir à mão.
    /// </summary>
    public IReadOnlyList<PendingMod> ManualUploads =>
        [.. PendingMods.Where(p => p.Reason is not PendingModReason.Queued)];

    public bool HasManualUploads => ManualUploads.Count > 0;

    /// <summary>
    ///     Quantas vezes o arranque já reenfileirou esta ingestão sozinho.
    ///     Existe para a recuperação automática não virar laço: se o que derruba
    ///     o processo é justamente este job, reenfileirar a cada arranque faz o
    ///     servidor cair em ciclo e nunca subir. Depois de
    ///     <see cref="MaxRecoveryAttempts" /> tentativas a versão para de ser
    ///     recuperada e o admin decide.
    /// </summary>
    public int RecoveryAttempts { get; set; }

    /// <summary>
    ///     Três é o suficiente para cobrir uma queda por causa externa (deploy no
    ///     meio, rede caindo) sem insistir num job que é ele próprio o problema.
    /// </summary>
    public const int MaxRecoveryAttempts = 3;

    /// <summary>Pendências que ainda esperam a fila, e não uma decisão do admin.</summary>
    public bool HasQueuedMods =>
        PendingMods.Any(p => p.Reason is PendingModReason.Queued);

    /// <summary>
    ///     Registra que o arranque assumiu esta ingestão de novo.
    ///     Devolve false quando o limite estourou — aí quem chama marca a falha
    ///     em vez de reenfileirar.
    /// </summary>
    public bool TryRegisterRecovery()
    {
        if (RecoveryAttempts >= MaxRecoveryAttempts)
            return false;

        RecoveryAttempts++;
        Touch();
        return true;
    }

    /// <summary>
    ///     Início da ingestão. Só faz sentido a partir de Draft ou de uma
    ///     tentativa que falhou.
    /// </summary>
    public void MarkResolving()
    {
        if (State is not (ModpackVersionState.Draft or ModpackVersionState.Failed))
        {
            throw new InvalidOperationException(
                $"Não é possível iniciar a resolução a partir do estado {State}.");
        }

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
        {
            throw new InvalidOperationException(
                $"Não é possível publicar a partir do estado {State}.");
        }

        if (Files.Count is 0)
            throw new InvalidOperationException("Não é possível publicar uma versão sem arquivos.");

        State = ModpackVersionState.Ready;
        PublishedAt = DateTimeOffset.UtcNow;

        // A ingestão chegou ao fim: o histórico de recuperações não vale mais
        // nada, e mantê-lo faria a próxima interrupção começar com a cota gasta.
        RecoveryAttempts = 0;

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
    ///     Volta uma versão que falhou para rascunho, para o admin corrigir e
    ///     tentar de novo na MESMA versão.
    ///     Sem isto, uma falha de resolução (um mod fora do ar, cota de API
    ///     estourada) obrigaria a jogar a versão inteira fora e recomeçar — e o
    ///     que já foi baixado com sucesso continua válido.
    /// </summary>
    public void RetryAfterFailure()
    {
        if (State is not ModpackVersionState.Failed)
            throw new InvalidOperationException($"Só é possível reparar uma versão que falhou. Estado atual: {State}.");

        State = ModpackVersionState.Draft;
        FailureReason = null;

        // Reparo pedido por gente é recomeço: a cota de recuperação automática
        // volta ao zero, senão uma versão que já esgotou o limite nunca mais
        // seria recuperada sozinha depois de consertada.
        RecoveryAttempts = 0;

        Touch();
    }

    /// <summary>
    ///     Não oferecer a novos clientes, mas manter funcionando para quem já usa.
    ///     Nunca apague uma versão que tenha servidor apontando para ela.
    /// </summary>
    public void Archive()
    {
        if (State is not ModpackVersionState.Ready)
        {
            throw new InvalidOperationException(
                $"Só é possível arquivar uma versão publicada. Estado atual: {State}.");
        }

        State = ModpackVersionState.Archived;
        Touch();
    }

    /// <summary>
    ///     Desfaz o arquivamento: volta uma versão arquivada a Ready, tornando-a
    ///     de novo oferecível a novos clientes. É o inverso de <see cref="Archive" />.
    /// </summary>
    public void Restore()
    {
        if (State is not ModpackVersionState.Archived)
        {
            throw new InvalidOperationException(
                $"Só é possível restaurar uma versão arquivada. Estado atual: {State}.");
        }

        State = ModpackVersionState.Ready;
        Touch();
    }

    /// <summary>
    ///     Volta de Resolving para Draft ao fim de uma ingestao bem-sucedida.
    ///     Resolver e baixar e uma coisa; publicar e decisao explicita do admin.
    ///     Sem isto a versao ficaria presa em Resolving ou publicaria sozinha.
    /// </summary>
    public void ReturnToDraft()
    {
        if (State is not ModpackVersionState.Resolving)
            throw new InvalidOperationException($"Nao e possivel voltar para rascunho a partir de {State}.");

        State = ModpackVersionState.Draft;
        FailureReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Adiciona um arquivo respeitando a identidade do mod.
    ///     Se já existe arquivo com o mesmo ProjectSlug, ele e removido da versao e
    ///     seu ID e devolvido — o chamador apaga a linha antiga no banco. Assim
    ///     atualizar/rebaixar um mod = trocar o .jar, nunca acumular dois em mods/
    ///     (isso crasharia o jogo). Sem ProjectSlug, a unicidade fica por Path,
    ///     checada no caso de uso.
    /// </summary>
    /// <summary>
    ///     Registra (ou atualiza) uma pendência. Chave é o ProjectSlug: reingerir
    ///     não pode acumular a mesma pendência duas vezes.
    /// </summary>
    public void UpsertPending(PendingMod pending)
    {
        pending.ModpackVersionId = Id;

        var existing = FindPending(pending.ProjectSlug);
        if (existing is not null)
        {
            // A pendência nova HERDA o Id da que substitui: para o banco é a
            // mesma linha mudando de motivo, não uma linha nova. Ver o porquê
            // em PendingMod.TakeOverFrom — é o que impede o INSERT duplicado
            // no índice único (ModpackVersionId, ProjectSlug).
            pending.TakeOverFrom(existing);
            PendingMods.Remove(existing);
        }

        PendingMods.Add(pending);
        Touch();
    }

    /// <summary>
    ///     Baixa a pendência quando o mod finalmente entrou (upload manual ou
    ///     nova tentativa bem-sucedida). Devolve o Id removido para o repositório
    ///     apagar a linha — grafo destacado não cascateia remoção de coleção.
    /// </summary>
    public Guid? ResolvePending(string projectSlug)
    {
        var existing = FindPending(projectSlug);
        if (existing is null)
            return null;

        PendingMods.Remove(existing);
        Touch();
        return existing.Id;
    }

    private PendingMod? FindPending(string projectSlug) =>
        PendingMods.FirstOrDefault(p =>
            string.Equals(p.ProjectSlug, projectSlug, StringComparison.OrdinalIgnoreCase));

    public Guid? UpsertFile(ModpackFile file)
    {
        if (State is not (ModpackVersionState.Draft or ModpackVersionState.Resolving))
            throw new InvalidOperationException($"Nao e possivel alterar arquivos a partir de {State}.");

        Guid? replacedId = null;

        if (file.ProjectSlug is { Length: > 0 } slug)
        {
            var existing = Files.FirstOrDefault(f =>
                string.Equals(f.ProjectSlug, slug, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                replacedId = existing.Id;
                Files.Remove(existing);
            }
        }

        file.ModpackVersionId = Id;
        Files.Add(file);
        UpdatedAt = DateTimeOffset.UtcNow;

        return replacedId;
    }
}
