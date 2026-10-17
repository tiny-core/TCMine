using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Identity;

/// <summary>
///     As opções de um jogador num modpack (teclas e, se ele quiser, o resto do
///     <c>options.txt</c>), guardadas na conta para voltarem em qualquer PC e em
///     qualquer instância do mesmo pack.
///     Por pack, e não por instância nem por servidor: as teclas dependem dos
///     mods, e os mods são do pack. A instância pessoal e a de um servidor do
///     mesmo pack partilham as mesmas teclas — que é o que o jogador espera.
/// </summary>
public sealed class PlayerModpackSettings : Entity
{
    /// <summary>
    ///     As linhas key_* de um pack grande somam poucos kilobytes; o teto só
    ///     existe para um cliente estragado (ou mal-intencionado) não encher a
    ///     base com o que chamar de teclas.
    /// </summary>
    public const int MaxKeyBindingsLength = 64 * 1024;

    /// <summary>O resto do options.txt, com folga para os packs que o enchem de opções de mods.</summary>
    public const int MaxOptionsLength = 256 * 1024;

    public required Guid UserId { get; set; }

    public required Guid ModpackId { get; set; }

    public string KeyBindings { get; private set; } = "";

    public string? Options { get; private set; }

    /// <summary>A hora da última gravação, que é a que o launcher compara.</summary>
    public DateTimeOffset LastSavedAt => UpdatedAt ?? CreatedAt;

    /// <summary>
    ///     Grava por cima: vale a gravação mais recente, que é a do último jogo
    ///     fechado. <paramref name="options" /> nulo mantém o que havia — é um PC
    ///     que só sincroniza teclas, e não deve apagar o que outro mandou.
    /// </summary>
    public void Replace(string keyBindings, string? options)
    {
        KeyBindings = keyBindings;

        if (options is not null)
            Options = options;

        Touch();
    }
}
