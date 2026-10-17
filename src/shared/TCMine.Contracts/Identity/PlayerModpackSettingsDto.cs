namespace TCMine.Contracts.Identity;

/// <summary>
///     As opções que um jogador leva consigo num modpack, de PC em PC e de
///     instância em instância.
///     <see cref="KeyBindings" /> são as linhas <c>key_*</c> do
///     <c>options.txt</c>, que é onde o Minecraft e a maioria dos mods guardam as
///     teclas. <see cref="Options" /> é o resto do arquivo, e só existe para quem
///     ligou a sincronização completa nalgum PC: gráficos e som costumam ser do
///     computador, não da pessoa.
///     Texto como o jogo escreve, e não um mapa já interpretado: quem lê e
///     funde é o launcher, que conhece o formato; o servidor só guarda.
/// </summary>
public sealed record PlayerModpackSettingsDto
{
    public required string KeyBindings { get; init; }

    public string? Options { get; init; }

    /// <summary>Hora do servidor da última gravação.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
