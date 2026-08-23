using TCMine.Contracts.Modpacks;
namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Lê os metadados de um .jar de mod.
///     Existe porque a versão de loader que um mod exige NÃO está em nenhuma das
///     duas APIs — nem o Modrinth nem o CurseForge a expõem. Ela vive dentro do
///     jar (<c>neoforge.mods.toml</c>, <c>mods.toml</c>, <c>fabric.mod.json</c>),
///     e é a única fonte confiável. Como o arquivo já passa pelas nossas mãos no
///     download, conferir ali sai de graça.
/// </summary>
public interface IModJarInspector
{
    /// <summary>
    ///     Devolve o que der para ler. Null quando o arquivo não é um jar de mod
    ///     reconhecível — o que NÃO é motivo para recusar nada.
    /// </summary>
    Task<ModJarInfo?> InspectAsync(Stream jar, CancellationToken ct);
}

/// <summary>
///     <paramref name="RequiredLoaderRange" /> na notação do próprio loader:
///     intervalo Maven no Forge/NeoForge (<c>[21.1.80,)</c>) ou predicado do
///     Fabric (<c>&gt;=0.15.0</c>). Null quando o mod não declara exigência.
/// </summary>
/// <summary>
///     <paramref name="DeclaredSide" /> só vem preenchido quando o JAR o declara,
///     o que hoje significa Fabric: o <c>fabric.mod.json</c> tem um campo
///     <c>environment</c> padronizado. O <c>neoforge.mods.toml</c> não tem campo
///     de lado por mod — o Colorwheel, que existe só para usar shaders no
///     cliente, declara todas as dependências como BOTH. Nulo é "o jar não diz",
///     e aí o lado continua sendo do server pack ou do admin.
/// </summary>
public sealed record ModJarInfo(
    string? ModId, string? RequiredLoaderRange, FileSide? DeclaredSide = null);
