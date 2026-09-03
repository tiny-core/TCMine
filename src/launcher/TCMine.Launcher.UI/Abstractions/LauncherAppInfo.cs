namespace TCMine.Launcher.UI.Abstractions;

/// <summary>
///     Identidade da build, para a UI exibir sem ler o próprio assembly.
///     Vem do host porque é ele que é publicado e versionado — a RCL das telas é
///     apenas compilada junto e não tem versão própria que signifique algo para
///     o jogador. Injetar como dado também deixa a barra de estado testável sem
///     um executável no meio.
/// </summary>
public sealed record LauncherAppInfo
{
    /// <summary>Nome exibido na barra de título.</summary>
    public required string Title { get; init; }

    /// <summary>Versão informativa da build, ex.: "0.1.0+abc1234".</summary>
    public required string Version { get; init; }
}
