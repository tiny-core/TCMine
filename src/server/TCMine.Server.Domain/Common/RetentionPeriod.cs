namespace TCMine.Server.Domain.Common;

/// <summary>
///     Prazo de retenção em dias, a mesma regra para todo prazo configurável:
///     zero é "manter para sempre"; fora isso, entre uma semana e dez anos.
///     O piso existe porque um prazo de 1 dia, digitado por engano, apagaria o
///     histórico de ontem na rodada seguinte — e retenção não tem desfazer.
/// </summary>
public static class RetentionPeriod
{
    public const int Forever = 0;

    public const int MinDays = 7;

    public const int MaxDays = 3650;

    public static bool IsValid(int days) => days == Forever || days is >= MinDays and <= MaxDays;

    public static string Rule => $"Use 0 para manter para sempre, ou de {MinDays} a {MaxDays} dias.";
}
