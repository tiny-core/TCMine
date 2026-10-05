namespace TCMine.Server.Domain.Common;

/// <summary>
///     Uma linha do feed "Atividade recente" da Visão geral. Guarda o texto já
///     pronto (e não, por exemplo, só o Id da versão publicada) porque o feed é
///     de leitura rápida e append-only: remontar a frase a partir de outras
///     tabelas a cada carga custaria joins caros para um painel que só quer
///     mostrar "o que aconteceu". <see cref="Kind" /> existe só para o ícone/cor
///     — a mensagem já é a fonte da verdade do texto.
/// </summary>
public sealed class ActivityEvent : Entity
{
    public required ActivityEventKind Kind { get; set; }

    public required string Message { get; set; }

    /// <summary>Link opcional para onde o evento aconteceu (servidor, modpack…).</summary>
    public string? Href { get; set; }
}

public enum ActivityEventKind
{
    UserLoggedIn,
    VersionPublished,
    ServerCrashed,
    WorldBackupCreated
}
