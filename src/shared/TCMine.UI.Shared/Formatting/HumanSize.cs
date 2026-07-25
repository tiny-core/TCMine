namespace TCMine.UI.Shared.Formatting;

/// <summary>
///     Formata tamanhos e contagens para exibição.
///     Vive na UI compartilhada porque tanto o painel do servidor quanto o
///     launcher mostram tamanho de arquivo — e "1.5 MB" precisa ser idêntico nos
///     dois, senão o mesmo mod aparece com formato diferente em cada tela.
/// </summary>
public static class HumanSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    ///     Bytes para a maior unidade que mantém o número legível.
    ///     Ex.: 1536 → "1.5 KB", 5_242_880 → "5.0 MB".
    /// </summary>
    public static string Bytes(long bytes)
    {
        if (bytes < 0)
            return "0 B";

        double value = bytes;
        var unit = 0;

        // Sobe de unidade enquanto o valor passar de 1024, parando na última
        // conhecida para não estourar o array com um arquivo absurdo.
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // Bytes puros não levam casa decimal — "512 B", não "512.0 B".
        return unit is 0
            ? $"{bytes} {Units[0]}"
            : $"{value:F1} {Units[unit]}";
    }
}