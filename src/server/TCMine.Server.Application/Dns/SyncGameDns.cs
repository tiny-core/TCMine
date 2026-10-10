using System.Net;
using System.Net.Sockets;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Dns;

/// <summary>
///     Faz a zona da Cloudflare ficar igual ao que os servidores pedem: um SRV
///     por servidor com subdomínio, e o registro A para onde eles apontam.
///     Só mexe no que tem a marca desta instalação
///     (<see cref="GameDns.CommentPrefix" />). Um registro que o admin criou à
///     mão nunca é alterado nem apagado — se ocupa um nome de que a
///     sincronização precisa, ela para naquele registro e diz qual é.
///     Não lança: o resultado é sempre um <see cref="DnsSyncReport" />, com os
///     problemas em texto. Uma Cloudflare fora do ar não pode derrubar quem
///     chamou, e cada registro falha sozinho — o resto da zona é acertado.
/// </summary>
public sealed class SyncGameDns(
    ISettingsRepository settings,
    IServerRepository servers,
    IPublicAddressProvider detector,
    ICloudflareDns dns)
{
    public async Task<DnsSyncReport> HandleAsync(CancellationToken ct)
    {
        var config = await settings.GetAsync(ct);

        // Desligado não apaga o que já foi criado: sem token não há como, e
        // tirar o token é justamente a forma de dizer "não mexa mais na zona".
        if (!config.DnsEnabled)
            return DnsSyncReport.Disabled;

        var zone = new DnsZone(config.CloudflareZoneId!.Trim(), config.CloudflareApiTokenEncrypted!.Trim());
        var baseDomain = GameDns.NormalizeLabel(config.DnsBaseDomain)!;
        var prefix = GameDns.CommentPrefix(config.Id);
        var hostComment = prefix + "host";

        var problems = new List<string>();
        var desired = new List<DnsRecordSpec>();
        string? keepComment = null;

        // ---- Para onde os SRV apontam ----
        // Um SRV aponta para um NOME, nunca para um IP. Se o endereço público da
        // instalação já é um nome (DDNS, domínio próprio), é ele. Senão o TCMine
        // mantém um registro A com o IP — o gravado, ou o detectado.
        var publicHost = string.IsNullOrWhiteSpace(config.PublicHost) ? null : config.PublicHost.Trim();
        var publicHostIsName = publicHost is not null && !IPAddress.TryParse(publicHost, out _);

        var hostLabel = GameDns.NormalizeLabel(config.DnsHostLabel) ?? GameDns.DefaultHostLabel;
        var managedHost = $"{hostLabel}.{baseDomain}";
        string target;

        // O nome gravado pode ser o próprio registro que o TCMine mantém (o admin
        // copiou "mc.exemplo.com" para o endereço público). Tratá-lo como um nome
        // de fora apagaria o registro A de que ele mesmo depende.
        if (publicHostIsName && !string.Equals(publicHost, managedHost, StringComparison.OrdinalIgnoreCase))
        {
            target = publicHost!;
        }
        else
        {
            target = managedHost;

            var ip = publicHostIsName ? await detector.GetAsync(ct) : publicHost ?? await detector.GetAsync(ct);

            if (ip is not null
                && IPAddress.TryParse(ip, out var parsed)
                && parsed.AddressFamily is AddressFamily.InterNetwork)
            {
                desired.Add(new DnsRecordSpec(DnsRecordTypes.A, target, parsed.ToString(), 0, hostComment));
            }
            else
            {
                // Sem IP não se sabe o que escrever no registro A — e também não
                // se sabe que ele está errado. Fica como está.
                keepComment = hostComment;
                problems.Add(ip is null
                    ? $"O IP público não foi detectado: o registro {target} não foi criado nem atualizado."
                    : $"O IP público {ip} não é IPv4, e o registro {target} precisa de um: ele não foi atualizado.");
            }
        }

        // ---- Um SRV por servidor com subdomínio ----
        foreach (var server in await servers.ListAllAsync(ct))
        {
            if (GameDns.ServerHost(server.Subdomain, baseDomain) is not { } host)
                continue;

            desired.Add(new DnsRecordSpec(
                DnsRecordTypes.Srv,
                GameDns.SrvName(host),
                target,
                server.GamePort,
                $"{prefix}srv:{server.Id:N}"));
        }

        IReadOnlyList<DnsRecord> existing;
        try
        {
            existing = await dns.ListByCommentPrefixAsync(zone, prefix, ct);
        }
        catch (DnsProviderException ex)
        {
            // Sem a lista não há diferença a calcular: nada é tocado.
            return DnsSyncReport.Failed(ex.Message);
        }

        var plan = DnsPlan.Build(desired, existing, keepComment);
        var deleted = 0;
        var replaced = 0;
        var created = 0;

        // Apagar primeiro: um servidor novo pode estar herdando o subdomínio de
        // um que acabou de ser apagado.
        foreach (var record in plan.Delete)
        {
            try
            {
                await dns.DeleteAsync(zone, record.Id, ct);
                deleted++;
            }
            catch (DnsProviderException ex)
            {
                problems.Add($"{record.Spec.Name}: não foi possível apagar. {ex.Message}");
            }
        }

        foreach (var change in plan.Replace)
        {
            try
            {
                await dns.ReplaceAsync(zone, change.RecordId, change.Record, ct);
                replaced++;
            }
            catch (DnsProviderException ex)
            {
                problems.Add($"{change.Record.Name}: não foi possível atualizar. {ex.Message}");
            }
        }

        foreach (var record in plan.Create)
        {
            try
            {
                // A Cloudflare aceita dois registros A com o mesmo nome, e passa a
                // responder ora um, ora outro. Criar por cima de um registro do
                // admin (ou de outra instalação) quebraria o que ele tem sem
                // nenhum erro à vista — por isso a conferência é nossa.
                var sameName = await dns.ListByNameAsync(zone, record.Name, ct);

                var occupant = sameName.FirstOrDefault(r =>
                    string.Equals(r.Spec.Type, record.Type, StringComparison.OrdinalIgnoreCase));

                if (occupant is not null)
                {
                    // Com a nossa marca é sobra de uma rodada que não conseguiu
                    // apagar: a próxima resolve. Sem ela, é de outra pessoa.
                    problems.Add(occupant.Spec.Comment.StartsWith(prefix, StringComparison.Ordinal)
                        ? $"{record.Name}: um registro antigo com este nome ainda não foi apagado. Sincronize de novo."
                        : $"{record.Name}: já existe um registro {record.Type} com este nome que não foi criado "
                          + "por este TCMine. Apague-o na Cloudflare ou escolha outro nome.");
                    continue;
                }

                await dns.CreateAsync(zone, record, ct);
                created++;
            }
            catch (DnsProviderException ex)
            {
                problems.Add($"{record.Name}: não foi possível criar. {ex.Message}");
            }
        }

        return new DnsSyncReport(true, created, replaced, deleted, plan.Unchanged, problems);
    }
}

/// <param name="Enabled">Falso quando falta token, zona ou domínio: nada foi feito.</param>
/// <param name="Problems">O que não deu certo, pronto para mostrar. Vazio = a zona está como deve.</param>
public sealed record DnsSyncReport(
    bool Enabled,
    int Created,
    int Updated,
    int Deleted,
    int Unchanged,
    IReadOnlyList<string> Problems)
{
    public static DnsSyncReport Disabled { get; } = new(false, 0, 0, 0, 0, Array.Empty<string>());

    public bool Succeeded => Enabled && Problems.Count == 0;

    public static DnsSyncReport Failed(string problem) => new(true, 0, 0, 0, 0, new[] { problem });
}
