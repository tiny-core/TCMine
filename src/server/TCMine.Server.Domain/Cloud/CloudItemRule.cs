using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Uma regra de item da nuvem: permitir ou bloquear um item, uma tag ou um
///     mod inteiro. O mod (tccloud) aplica com a precedência dele: o mais
///     específico vence (item &gt; tag &gt; mod &gt; modo da nuvem) e, no mesmo nível,
///     bloquear vence. Mudar regra aumenta a <see cref="CloudVault.PolicyVersion" />
///     (quem cria/apaga a regra é que chama).
/// </summary>
public sealed class CloudItemRule : Entity
{
    public const int PatternMaxLength = 256;

    public required Guid VaultId { get; init; }

    public required CloudRuleScope Scope { get; init; }

    /// <summary><c>mod:item</c>, <c>mod:tag</c> (sem #) ou <c>mod</c>; sempre minúsculo.</summary>
    public required string Pattern { get; init; }

    public required CloudRuleAction Action { get; init; }

    public string? Note { get; init; }

    public Guid? CreatedByUserId { get; init; }

    /// <summary>Normaliza e valida o padrão para o escopo. Nulo = inválido.</summary>
    public static string? NormalizePattern(CloudRuleScope scope, string? raw)
    {
        var p = raw?.Trim().ToLowerInvariant().TrimStart('#');
        if (string.IsNullOrEmpty(p) || p.Length > PatternMaxLength || p.Any(char.IsWhiteSpace))
            return null;
        var hasNamespace = p.Contains(':');
        return scope switch
        {
            CloudRuleScope.Mod => hasNamespace ? null : p,
            _ => hasNamespace && !p.StartsWith(':') && !p.EndsWith(':') ? p : null
        };
    }
}

public enum CloudRuleScope
{
    Item,
    Tag,
    Mod
}

public enum CloudRuleAction
{
    Allow,
    Block
}
