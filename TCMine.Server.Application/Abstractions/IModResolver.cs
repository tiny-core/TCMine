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
    /// </summary>
    bool IsAvailable { get; }

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
    public sealed record Resolved(
        string FileName,
        string? Sha1,
        long SizeBytes,
        Uri DownloadUrl) : ModResolution;

    /// <summary>
    ///     O autor marcou allowModDistribution = false.
    ///     Não é bloqueio de cota nem erro: é o mecanismo de opt-out do autor, e
    ///     não existe contorno legítimo. A publicação deve falhar listando os
    ///     mods afetados para o admin decidir o que fazer.
    /// </summary>
    public sealed record DistributionDenied(string ProjectName, Uri ProjectPage) : ModResolution;

    public sealed record NotFound(string Reason) : ModResolution;
}