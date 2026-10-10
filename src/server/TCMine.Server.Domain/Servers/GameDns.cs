using System.Globalization;
using System.Text;

namespace TCMine.Server.Domain.Servers;

/// <summary>
///     Nomes de DNS dos servidores de jogo — regras puras, sem rede.
///     Um servidor com subdomínio é publicado como <c>sub.dominio</c>, sem porta:
///     quem leva a porta é um registro SRV (<c>_minecraft._tcp.sub.dominio</c>),
///     que o Minecraft Java consulta sozinho antes de conectar. É o que permite
///     vários servidores na mesma máquina, cada um com um nome limpo.
/// </summary>
public static class GameDns
{
    /// <summary>Limite de um rótulo de DNS (o trecho entre dois pontos).</summary>
    public const int MaxLabelLength = 63;

    /// <summary>Rótulo do registro A que o TCMine mantém quando ninguém escolheu outro.</summary>
    public const string DefaultHostLabel = "mc";

    public const string InvalidLabelMessage =
        "O subdomínio aceita só letras sem acento, números e hífen, sem começar nem terminar em hífen "
        + "(ex.: sobrevivencia).";

    /// <summary>O rótulo como é guardado: sem espaços e em minúsculas. Vazio vira nulo.</summary>
    public static string? NormalizeLabel(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToLowerInvariant();

    /// <summary>
    ///     Um rótulo de DNS válido, já normalizado: letras a–z, dígitos e hífen,
    ///     sem hífen nas pontas. Um ponto não entra — "a.b" seria dois níveis, e o
    ///     subdomínio de um servidor é um só.
    /// </summary>
    public static bool IsValidLabel(string? label) =>
        label is { Length: > 0 and <= MaxLabelLength }
        && label[0] != '-'
        && label[^1] != '-'
        && label.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    /// <summary>Um domínio completo ("exemplo.com", "jogos.exemplo.com"): dois rótulos válidos ou mais.</summary>
    public static bool IsValidDomain(string? domain)
    {
        var labels = (domain ?? "").Split('.');

        return labels.Length >= 2 && labels.All(IsValidLabel);
    }

    /// <summary>
    ///     Um rótulo sugerido a partir do nome do servidor: "ATM 10 Server" vira
    ///     "atm-10-server". Só sugestão — nada é publicado sem o admin preencher.
    /// </summary>
    public static string Suggest(string? serverName)
    {
        var builder = new StringBuilder();

        // FormD separa a letra do acento, e o acento (NonSpacingMark) é
        // descartado: "Sobrevivência" vira "sobrevivencia", não "sobreviv-ncia".
        foreach (var c in (serverName ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark)
                continue;

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
                builder.Append(c);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        var label = builder.ToString().Trim('-');

        return label.Length > MaxLabelLength ? label[..MaxLabelLength].Trim('-') : label;
    }

    /// <summary>
    ///     O nome pelo qual os jogadores entram: <c>sub.dominio</c>. Nulo quando o
    ///     servidor não tem subdomínio ou a instalação não tem domínio — os dois
    ///     são precisos para haver um nome.
    /// </summary>
    public static string? ServerHost(string? subdomain, string? baseDomain)
    {
        var label = NormalizeLabel(subdomain);
        var domain = NormalizeLabel(baseDomain);

        return label is null || domain is null ? null : $"{label}.{domain}";
    }

    /// <summary>O nome do registro SRV que o Minecraft consulta para um host.</summary>
    public static string SrvName(string serverHost) => $"_minecraft._tcp.{serverHost}";

    /// <summary>O outro servidor que já usa este subdomínio, se houver.</summary>
    public static GameServer? OwnerOf(string label, IEnumerable<GameServer> servers, Guid? exceptServerId) =>
        servers.FirstOrDefault(s =>
            s.Id != exceptServerId && string.Equals(s.Subdomain, label, StringComparison.Ordinal));

    /// <summary>
    ///     O começo do comentário que marca, na Cloudflare, os registros DESTA
    ///     instalação. É por ele que a sincronização sabe o que é dela: tudo o que
    ///     não começa assim nunca é alterado nem apagado.
    ///     Leva o id da instalação porque duas podem partilhar a zona — a de
    ///     produção e a de desenvolvimento, por exemplo — e uma não pode apagar os
    ///     registros da outra. São os últimos 12 caracteres do id, a parte
    ///     aleatória de um GUID v7: o começo é data, e duas instalações criadas
    ///     no mesmo minuto teriam o mesmo.
    /// </summary>
    public static string CommentPrefix(Guid installationId) =>
        $"tcmine:{installationId.ToString("N")[^12..]}:";
}
