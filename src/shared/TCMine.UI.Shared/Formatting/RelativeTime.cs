using System.Globalization;

namespace TCMine.UI.Shared.Formatting;

/// <summary>
///     "há 5 min", não o timestamp cru — um feed de atividade se lê de relance
///     pela distância no tempo, não pela hora exata. Cai para a data quando
///     passa de uma semana: "há 34 dias" não diz mais nada que a data não diga
///     melhor.
/// </summary>
public static class RelativeTime
{
    public static string From(DateTimeOffset moment, DateTimeOffset? now = null)
    {
        var agora = now ?? DateTimeOffset.UtcNow;
        var delta = agora - moment;

        if (delta < TimeSpan.Zero)
            delta = TimeSpan.Zero; // relógio do cliente atrasado em relação ao servidor; não mostra "no futuro"

        return delta switch
        {
            { TotalSeconds: < 60 } => "agora mesmo",
            { TotalMinutes: < 60 } => $"há {(int)delta.TotalMinutes} min",
            { TotalHours: < 24 } => $"há {(int)delta.TotalHours} h",
            { TotalDays: < 7 } => $"há {(int)delta.TotalDays} dia(s)",
            _ => moment.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
        };
    }
}
