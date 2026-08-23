using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Descobre onde baixar um mod.
///     Teremos um resolver por origem. A ordem de tentativa importa: Modrinth
///     primeiro, porque não exige API key e a licença de publicação de lá já
///     permite redistribuição. CurseForge só como segunda opção.
/// </summary>
public interface IModResolver
{
    ModFileOrigin Origin { get; }

    /// <summary>
    ///     Está configurado e pronto para uso? CurseForge sem API key devolve
    ///     false, e o sistema segue funcionando só com Modrinth — não force
    ///     ninguém a criar conta no CurseForge para usar o TCMine.
    ///     Assíncrono porque a chave vive na configuração da instalação (banco),
    ///     e pode ser trocada em tempo de execução pelo painel.
    /// </summary>
    ValueTask<bool> IsAvailableAsync(CancellationToken ct);

    Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct);
}

public sealed record ModRequest(
    string ProjectId,
    string? FileId,
    string MinecraftVersion,
    ModLoader Loader);

/// <summary>
///     Resultado da resolução, modelado como união de casos.
///     A vantagem sobre devolver null ou lançar exceção: o compilador te obriga
///     a tratar cada situação, e cada uma carrega exatamente os dados que
///     precisa. "Não achei" e "o autor proibiu" pedem respostas diferentes na UI.
/// </summary>
public abstract record ModResolution
{
    private ModResolution()
    {
    }

    /// <summary>Encontrado e liberado para download.</summary>
    /// <summary>
    ///     <paramref name="Side" /> só vem preenchido quando a ORIGEM declara em
    ///     que lado o mod roda (hoje só o Modrinth). Nulo significa "a origem não
    ///     sabe" — e aí vale o lado pedido na ingestão, nunca um chute.
    /// </summary>
    public sealed record Resolved(
        string VersionId,
        string FileName,
        string? Sha1,
        long SizeBytes,
        Uri DownloadUrl,
        IReadOnlyList<ModDependency> Dependencies,
        string? IconUrl = null,
        FileSide? Side = null,

        /// <summary>
        ///     Pasta da instância onde este arquivo vive. Nem tudo o que vem num
        ///     modpack é mod: um shaderpack em mods/ derruba o jogo no arranque.
        /// </summary>
        string Folder = "mods") : ModResolution;

    /// <summary>
    ///     O autor marcou allowModDistribution = false.
    ///     Não é bloqueio de cota nem erro: é o mecanismo de opt-out do autor, e
    ///     não existe contorno legítimo. A publicação deve falhar listando os
    ///     mods afetados para o admin decidir o que fazer.
    /// </summary>
    public sealed record DistributionDenied(
        string ProjectName, Uri ProjectPage, string Folder = "mods") : ModResolution;

    public sealed record NotFound(string Reason, string Folder = "mods") : ModResolution;
}

/// <summary>Dependência declarada por uma versão de projeto na origem.</summary>
public sealed record ModDependency(string ProjectId, ModDependencyKind Kind);

public enum ModDependencyKind
{
    Required,
    Optional,
    Incompatible,
    Embedded
}
