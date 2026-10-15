using MudBlazor;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Web.Components.Features.Network;

/// <summary>
///     O que se edita sobre a rede: o modo, a Cloudflare, a rede de casa e as
///     portas. Um objeto, e não campos soltos de uma página, porque são DUAS
///     telas a editar a mesma coisa — a aba Rede das configurações e o
///     assistente de primeira configuração — e as duas têm de gravar e prever
///     o endereço pela mesma regra.
///     Os componentes editam a mesma instância; quem a criou grava com
///     <see cref="ApplyTo" />.
/// </summary>
public sealed class NetworkForm
{
    public NetworkMode Mode { get; set; } = NetworkMode.Automatic;
    public bool CloudflareUsesPublicHost { get; set; }

    /// <summary>
    ///     Abre o "Avançado" só se a opção já estava ligada ao carregar. Ligado
    ///     ao valor vivo, desmarcar a caixa fecharia o painel na mão do admin.
    /// </summary>
    public bool AdvancedInitiallyOpen { get; private set; }

    /// <summary>Volta preenchido: é público, e o admin precisa conferi-lo.</summary>
    public string PublicHost { get; set; } = "";

    public string LanHost { get; set; } = "";

    /// <summary>Só se sabe se existe — o token nunca volta para a tela.</summary>
    public bool HasCloudflareToken { get; private set; }

    /// <summary>Novo token digitado. Vazio = manter o que já está gravado.</summary>
    public string CloudflareToken { get; set; } = "";

    public bool ClearCloudflareToken { get; set; }
    public string CloudflareZoneId { get; set; } = "";
    public string DnsBaseDomain { get; set; } = "";
    public string DnsHostLabel { get; set; } = "";
    public bool DnsManualSyncOnly { get; set; }
    public int DnsSyncIntervalMinutes { get; set; } = 5;
    public bool PublicIpDetectionDisabled { get; set; }
    public int GamePortRangeStart { get; set; } = GamePortDefaults.First;
    public int GamePortRangeEnd { get; set; } = GamePortDefaults.Last;

    /// <summary>IP público detectado. Nulo enquanto detecta e quando não se detectou.</summary>
    public string? DetectedAddress { get; set; }

    /// <summary>Começa ligado: a detecção roda depois do primeiro render, por quem hospeda o formulário.</summary>
    public bool IsDetecting { get; set; } = true;

    /// <summary>
    ///     Os três cartões. Uma lista, e não três blocos de markup: título,
    ///     ícone e explicação mudam juntos, e um cartão a mais é uma linha.
    /// </summary>
    public static IReadOnlyList<NetworkOption> Options { get; } =
    [
        new(NetworkMode.Automatic, Icons.Material.Filled.AutoMode, "Automático",
            "O IP desta casa, detectado sozinho. Nada a configurar; o endereço muda quando o IP mudar."),
        new(NetworkMode.OwnHost, Icons.Material.Filled.Language, "Meu domínio ou DDNS",
            "Um nome que você já mantém apontado para esta casa (ex.: um DDNS). O endereço não muda."),
        new(NetworkMode.Cloudflare, Icons.Material.Filled.Cloud, "Domínio na Cloudflare",
            "O TCMine cuida do DNS: cada servidor ganha um nome próprio, sem porta.")
    ];

    /// <summary>
    ///     O formulário a partir do que está gravado. O modo é o que VALE, e não
    ///     o gravado: numa instalação de antes da escolha o gravado é nulo, e o
    ///     cartão marcado tem de ser o que os jogadores estão recebendo hoje.
    /// </summary>
    public static NetworkForm From(InstallationSettings settings)
    {
        // Normalizada: uma faixa inválida no banco aparece como a padrão, que é
        // a que o alocador está usando de fato.
        var (start, end) = GamePortRange.Normalize(settings.GamePortRangeStart, settings.GamePortRangeEnd);

        var usesPublicHost = settings.EffectiveCloudflareUsesPublicHost;

        return new NetworkForm
        {
            Mode = settings.EffectiveNetworkMode,
            CloudflareUsesPublicHost = usesPublicHost,
            AdvancedInitiallyOpen = usesPublicHost,
            PublicHost = settings.PublicHost ?? "",
            LanHost = settings.LanHost ?? "",
            HasCloudflareToken = !string.IsNullOrEmpty(settings.CloudflareApiTokenEncrypted),
            CloudflareZoneId = settings.CloudflareZoneId ?? "",
            DnsBaseDomain = settings.DnsBaseDomain ?? "",
            DnsHostLabel = settings.DnsHostLabel ?? "",
            DnsManualSyncOnly = settings.DnsManualSyncOnly,

            // O que vale: um valor fora da lista no banco aparece como o
            // intervalo que o serviço está de fato usando.
            DnsSyncIntervalMinutes = (int)settings.DnsSyncInterval.TotalMinutes,
            PublicIpDetectionDisabled = settings.PublicIpDetectionDisabled,
            GamePortRangeStart = start,
            GamePortRangeEnd = end
        };
    }

    /// <summary>
    ///     Pergunta o IP público (respeitando o interruptor da detecção). Quem
    ///     hospeda chama depois do primeiro render: sai para a rede, e uma rede
    ///     lenta não pode segurar a tela nem o pré-render.
    /// </summary>
    public async Task DetectAsync(GetAddressSettings lookup)
    {
        try
        {
            DetectedAddress = (await lookup.HandleAsync(true, CancellationToken.None)).DetectedHost;
        }
        finally
        {
            IsDetecting = false;
        }
    }

    /// <summary>
    ///     Põe o formulário por cima de um comando. O resto do comando — o que
    ///     não é rede — fica como veio: é o que deixa o assistente gravar só a
    ///     parte dele partindo de <see cref="UpdateSettingsCommand.From" />.
    /// </summary>
    public UpdateSettingsCommand ApplyTo(UpdateSettingsCommand command) => command with
    {
        GamePortRangeStart = GamePortRangeStart,
        GamePortRangeEnd = GamePortRangeEnd,
        NetworkMode = Mode,
        CloudflareUsesPublicHost = Mode is NetworkMode.Cloudflare && CloudflareUsesPublicHost,
        PublicHost = PublicHost,
        LanHost = LanHost,
        CloudflareApiToken = CloudflareToken,
        ClearCloudflareApiToken = ClearCloudflareToken,
        CloudflareZoneId = CloudflareZoneId,
        DnsBaseDomain = DnsBaseDomain,
        DnsHostLabel = DnsHostLabel,
        DnsManualSyncOnly = DnsManualSyncOnly,
        DnsSyncIntervalMinutes = DnsSyncIntervalMinutes,
        PublicIpDetectionDisabled = PublicIpDetectionDisabled
    };

    /// <summary>
    ///     O nome do registro A como ficará, para o admin ver o que vai aparecer
    ///     na zona dele antes de salvar.
    /// </summary>
    public string DnsHostPreview =>
        GameDns.ManagedHost(DnsHostLabel, string.IsNullOrWhiteSpace(DnsBaseDomain) ? "seu-dominio.com" : DnsBaseDomain)!;

    /// <summary>
    ///     A prévia "O que os jogadores recebem", com o que está DIGITADO. Monta
    ///     uma configuração temporária e passa pela mesma
    ///     <see cref="AddressSettings.From" /> que publica o endereço de verdade.
    /// </summary>
    public IEnumerable<NetworkPreviewLine> Preview()
    {
        var draft = new InstallationSettings
        {
            NetworkMode = Mode,
            CloudflareUsesPublicHost = CloudflareUsesPublicHost,
            PublicHost = PublicHost,
            LanHost = LanHost,

            // Só importa SE haverá token, não qual: o valor nunca volta à tela.
            CloudflareApiTokenEncrypted =
                !ClearCloudflareToken && (HasCloudflareToken || !string.IsNullOrWhiteSpace(CloudflareToken))
                    ? "token"
                    : null,
            CloudflareZoneId = CloudflareZoneId,
            DnsBaseDomain = DnsBaseDomain,
            DnsHostLabel = DnsHostLabel
        };

        var address = AddressSettings.From(draft, DetectedAddress);
        var port = GamePortRangeStart;

        // Modo incompleto não cai no IP na prévia: o Salvar o recusaria, e
        // mostrar o IP faria parecer que está tudo certo.
        var incomplete = Mode switch
        {
            NetworkMode.OwnHost when string.IsNullOrWhiteSpace(PublicHost) => "preencha o domínio acima",
            NetworkMode.Cloudflare when !draft.HasCloudflareCredentials => "complete a Cloudflare acima",
            NetworkMode.Cloudflare when CloudflareUsesPublicHost && string.IsNullOrWhiteSpace(PublicHost) =>
                "preencha o DDNS ou IP fixo no Avançado",
            _ => null
        };

        var missing = incomplete ?? (IsDetecting ? "detectando o IP…" : "IP público não detectado");

        yield return new NetworkPreviewLine(
            $"Servidor na porta {port}", incomplete is null ? address.For(null, port, null) : null, missing);

        if (Mode is NetworkMode.Cloudflare)
        {
            yield return new NetworkPreviewLine(
                "Servidor com subdomínio \"sobrevivencia\"",
                incomplete is null ? address.For(null, port, "sobrevivencia") : null,
                missing);
        }

        if (address.LanHost is not null)
            yield return new NetworkPreviewLine("Quem está em casa", GameAddress.WithPort(address.LanHost, port), "");
    }
}

public sealed record NetworkOption(NetworkMode Mode, string Icon, string Title, string Description);

/// <param name="Missing">O que dizer quando ainda não há endereço.</param>
public sealed record NetworkPreviewLine(string Label, string? Address, string Missing);
