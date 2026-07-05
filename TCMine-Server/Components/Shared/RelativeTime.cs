namespace TCMine_Server.Components.Shared;

/// <summary>
///     Formatação de tempo relativo curto em PT-BR (ex.: "agora", "há 5 min", "há 2 h", "há 3 d").
///     Compartilhado pelos widgets do dashboard (atividade recente, modpacks recentes).
/// </summary>
public static class RelativeTime
{
    public static string Format(DateTime utc)
    {
        var diff = DateTime.UtcNow - utc;
        if (diff.TotalSeconds < 60) return "agora";
        if (diff.TotalMinutes < 60) return $"há {(int)diff.TotalMinutes} min";
        if (diff.TotalHours < 24) return $"há {(int)diff.TotalHours} h";
        if (diff.TotalDays < 30) return $"há {(int)diff.TotalDays} d";
        return utc.ToLocalTime().ToString("dd/MM/yyyy");
    }
}