using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Devolve a exigência de loader que o teste quiser. Sem argumento, não
///     declara nada — que é o caso da maioria dos jars e o caminho "pode passar".
/// </summary>
public sealed class FakeJarInspector(string? requiredLoaderRange = null) : IModJarInspector
{
    public Task<ModJarInfo?> InspectAsync(Stream jar, CancellationToken ct) =>
        Task.FromResult<ModJarInfo?>(new ModJarInfo("mod", requiredLoaderRange));
}
