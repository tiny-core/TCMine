using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Modpacks;

/// <summary>
///     Um mod que a ingestão não conseguiu trazer, mas que não invalida a versão.
///     O caso dominante é o autor marcar <c>allowModDistribution = false</c> no
///     CurseForge: não há solução técnica legítima, e é comum — o All the Mods 10
///     traz uma dúzia deles. Reprovar a versão inteira por isso deixaria packs
///     grandes eternamente impublicáveis; registrar a pendência deixa o admin
///     subir o .jar à mão (é o que os launchers oficiais fazem: abrem a página do
///     mod e o jogador baixa).
/// </summary>
public sealed class PendingMod : Entity
{
    public required Guid ModpackVersionId { get; set; }

    /// <summary>Identidade estável do mod — casa com <see cref="ModpackFile.ProjectSlug" />.</summary>
    public required string ProjectSlug { get; set; }

    /// <summary>Nome legível quando a origem informou; senão, o próprio id.</summary>
    public required string DisplayName { get; set; }

    public required ModFileOrigin Origin { get; set; }

    /// <summary>Release fixada pelo pack de origem, quando havia.</summary>
    public string? FileId { get; set; }

    public required PendingModReason Reason { get; set; }

    /// <summary>Texto da origem, para o admin entender sem abrir log.</summary>
    public string? Detail { get; set; }

    /// <summary>Página do mod na origem — o admin baixa o .jar por ela.</summary>
    public string? PageUrl { get; set; }

    public FileSide Side { get; set; } = FileSide.Both;
}

public enum PendingModReason
{
    /// <summary>Autor não permite redistribuição. Não adianta tentar de novo.</summary>
    DistributionDenied,

    /// <summary>Sem arquivo para esta versão do Minecraft/loader.</summary>
    NoCompatibleFile,

    /// <summary>Falhou o download ou a origem estava fora do ar — vale tentar de novo.</summary>
    Transient,

    /// <summary>
    ///     Pedido pelo admin, ainda não tentado.
    ///     Gravado no momento de enfileirar, e não depois de falhar como as
    ///     demais razões: a fila vive em memória, então sem esta linha um mod
    ///     escolhido a mão desaparecia sem rastro se o processo caísse antes de
    ///     o worker chegar nele — nem o reparo sabia que ele tinha sido pedido.
    ///     Some sozinha quando o mod resolve (ResolvePending) ou vira outra razão
    ///     quando falha (UpsertPending troca por slug).
    /// </summary>
    Queued
}
