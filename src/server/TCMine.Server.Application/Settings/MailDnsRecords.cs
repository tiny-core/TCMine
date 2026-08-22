namespace TCMine.Server.Application.Settings;

/// <summary>
///     Os registros que o domínio precisa publicar para o e-mail enviado daqui
///     ser aceito por Gmail, Outlook e companhia.
///     Função pura e num lugar só porque isto é a parte que o TCMine NÃO
///     controla: subir o servidor é o passo fácil, e sem estes registros a
///     mensagem sai daqui e é classificada como spam do outro lado. Melhor
///     entregar o texto pronto para colar que descrever o que fazer.
/// </summary>
public static class MailDnsRecords
{
    /// <summary>
    ///     Monta a lista para o domínio. O DKIM só entra quando a chave já foi
    ///     gerada — antes disso o servidor ainda não tem o que publicar, e um
    ///     registro com valor inventado seria pior que registro nenhum.
    /// </summary>
    public static IReadOnlyList<MailDnsRecord> For(string domain, string? dkimValue, string? publicIp)
    {
        var dominio = domain.Trim().TrimEnd('.').ToLowerInvariant();

        List<MailDnsRecord> registros =
        [
            new("TXT", dominio, $"v=spf1 a mx ~all",
                "Diz quais máquinas podem enviar em nome do domínio. Sem ele a mensagem "
                + "chega sem credencial nenhuma de origem."),

            new("TXT", $"_dmarc.{dominio}", "v=DMARC1; p=none; rua=mailto:postmaster@" + dominio,
                "Política de alinhamento e endereço para relatórios. Comece com p=none "
                + "para observar antes de endurecer para quarantine ou reject.")
        ];

        if (dkimValue is { Length: > 0 })
        {
            registros.Insert(1, new MailDnsRecord(
                "TXT", $"mail._domainkey.{dominio}", dkimValue,
                "Chave pública da assinatura DKIM. É o que prova que a mensagem saiu "
                + "deste servidor e não foi alterada no caminho."));
        }

        // MX aponta para onde o domínio RECEBE. Não é obrigatório para enviar,
        // mas a ausência é sinal de domínio descartável para boa parte dos
        // filtros — e sem ele nem uma resposta de erro voltaria.
        registros.Add(new MailDnsRecord(
            "MX", dominio, $"10 mail.{dominio}",
            "Para onde o domínio recebe. Opcional para enviar, mas a falta pesa "
            + "contra a reputação e impede receber devoluções."));

        registros.Add(new MailDnsRecord(
            "A", $"mail.{dominio}", publicIp ?? "(o IP público desta máquina)",
            "O nome que o servidor apresenta. Configure também o DNS REVERSO (PTR) "
            + "desse IP para este mesmo nome — isso é feito no painel de quem "
            + "fornece o IP, não aqui."));

        return registros;
    }
}

/// <summary>Um registro para colar no painel de DNS.</summary>
public sealed record MailDnsRecord(string Type, string Name, string Value, string Why);
