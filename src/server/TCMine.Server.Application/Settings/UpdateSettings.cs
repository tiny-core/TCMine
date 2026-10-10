using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Settings;

/// <summary>
///     Grava a configuração da instalação.
///     Segredos seguem a regra "vazio = manter": a UI nunca recebe o valor atual
///     de volta, então um campo em branco significa "não mexi nisso", e não
///     "apague". Para remover de fato existe a flag explícita de limpeza.
/// </summary>
public sealed class UpdateSettings(ISettingsRepository repository)
{
    /// <summary>O limite de um nome DNS completo; é também o tamanho da coluna.</summary>
    public const int PublicHostMaxLength = 255;

    public async Task<Result> HandleAsync(UpdateSettingsCommand command, CancellationToken ct)
    {
        if (command.DefaultMemoryMb is < 512)
            return Result.Fail("A RAM padrão precisa ser de pelo menos 512 MB.");

        if (command.WorldBackupKeepCount is < 0)
            return Result.Fail("A retenção de backups não pode ser negativa.");

        // A faixa só decide a porta SUGERIDA a servidores novos. Uma faixa
        // inválida não quebraria nada à vista — o alocador cairia na padrão em
        // silêncio — e o admin ficaria sem entender por que o número que gravou
        // não vale. Recusar aqui é o que torna o campo confiável.
        if (!GamePortRange.IsValid(command.GamePortRangeStart) || !GamePortRange.IsValid(command.GamePortRangeEnd))
        {
            return Result.Fail(
                $"As portas da faixa devem ficar entre {GamePortRange.Min} e {GamePortRange.Max}.");
        }

        if (command.GamePortRangeStart > command.GamePortRangeEnd)
            return Result.Fail("A primeira porta da faixa não pode ser maior que a última.");

        // Só o host. A porta é a de cada servidor, e qualquer coisa a mais aqui
        // (http://, uma barra, uma porta) viraria parte do endereço que o jogo
        // recebe — o erro apareceria no jogador, como "servidor desconhecido".
        var publicHost = Trimmed(command.PublicHost);

        if (publicHost is not null && !GameAddress.IsBareHost(publicHost))
        {
            return Result.Fail(
                "O endereço público é só o host: um domínio ou IP, sem http://, sem barra e sem porta. "
                + "A porta é a de cada servidor.");
        }

        if (publicHost is { Length: > PublicHostMaxLength })
            return Result.Fail($"O endereço público pode ter no máximo {PublicHostMaxLength} caracteres.");

        // ---- DNS (Cloudflare) ----
        // Os três são validados mesmo com o DNS incompleto: um valor errado
        // gravado hoje vira erro da Cloudflare no dia em que o token chegar.
        var zoneId = Trimmed(command.CloudflareZoneId);

        if (zoneId is not null && !(zoneId.Length == 32 && zoneId.All(Uri.IsHexDigit)))
        {
            return Result.Fail(
                "O Zone ID da Cloudflare tem 32 caracteres hexadecimais — está na página Overview do "
                + "domínio, na coluna da direita. Não é o nome do domínio nem o Account ID.");
        }

        var dnsBaseDomain = GameDns.NormalizeLabel(command.DnsBaseDomain);

        if (dnsBaseDomain is not null && !GameDns.IsValidDomain(dnsBaseDomain))
        {
            return Result.Fail(
                "O domínio dos servidores é um nome como exemplo.com ou jogos.exemplo.com, sem http:// e "
                + "sem porta.");
        }

        if (dnsBaseDomain is { Length: > PublicHostMaxLength })
            return Result.Fail($"O domínio dos servidores pode ter no máximo {PublicHostMaxLength} caracteres.");

        var dnsHostLabel = GameDns.NormalizeLabel(command.DnsHostLabel);

        if (dnsHostLabel is not null && !GameDns.IsValidLabel(dnsHostLabel))
        {
            return Result.Fail(
                "O nome do registro do IP aceita só letras sem acento, números e hífen (ex.: mc).");
        }

        // Um id malformado só se manifestaria na máquina do jogador, como uma
        // falha de login sem explicação: o handshake entrega o lixo, o MSAL
        // tenta montar a autoridade com ele e desiste. Recusar aqui move o erro
        // para quem consegue corrigi-lo.
        var azureClientId = Trimmed(command.AzureClientId);

        if (azureClientId is not null && !Guid.TryParse(azureClientId, out _))
        {
            return Result.Fail(
                "O client ID do Azure precisa ser um GUID — é o campo \"ID do aplicativo (cliente)\" "
                + "do registro no Entra ID, não o nome da app nem o ID de objeto.");
        }

        var settings = await repository.GetAsync(ct);

        settings.DefaultMinecraftVersion = string.IsNullOrWhiteSpace(command.DefaultMinecraftVersion)
            ? null
            : command.DefaultMinecraftVersion.Trim();
        settings.DefaultLoader = command.DefaultLoader;
        settings.DefaultMemoryMb = command.DefaultMemoryMb;
        settings.WorldBackupKeepCount = command.WorldBackupKeepCount;
        settings.GamePortRangeStart = command.GamePortRangeStart;
        settings.GamePortRangeEnd = command.GamePortRangeEnd;

        // Como o client ID abaixo: é público e volta para a tela preenchido,
        // então vazio é "apague" (volta a valer o IP detectado).
        settings.PublicHost = publicHost;

        // Públicos como o endereço acima: voltam preenchidos, e vazio apaga.
        settings.CloudflareZoneId = zoneId;
        settings.DnsBaseDomain = dnsBaseDomain;
        settings.DnsHostLabel = dnsHostLabel;

        // Não segue a regra "vazio = manter" do segredo abaixo: este valor é
        // público, volta para a tela preenchido, e portanto apagá-lo é um gesto
        // deliberado do admin — não um campo que ele não teve como preencher.
        settings.AzureClientId = azureClientId;

        // O segredo vai em claro para o repositório, que cifra ao gravar.
        if (command.ClearCurseForgeApiKey)
            settings.CurseForgeApiKeyEncrypted = null;
        else if (!string.IsNullOrWhiteSpace(command.CurseForgeApiKey))
            settings.CurseForgeApiKeyEncrypted = command.CurseForgeApiKey.Trim();

        // O token segue a regra dos segredos: vazio = manter.
        if (command.ClearCloudflareApiToken)
            settings.CloudflareApiTokenEncrypted = null;
        else if (!string.IsNullOrWhiteSpace(command.CloudflareApiToken))
            settings.CloudflareApiTokenEncrypted = command.CloudflareApiToken.Trim();

        await repository.SaveAsync(settings, ct);
        return Result.Success();
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record UpdateSettingsCommand
{
    public string? DefaultMinecraftVersion { get; init; }
    public ModLoader DefaultLoader { get; init; } = ModLoader.NeoForge;
    public int DefaultMemoryMb { get; init; } = 4096;

    /// <summary>Backups automáticos a manter por servidor. Zero = ilimitado.</summary>
    public int WorldBackupKeepCount { get; init; } = 5;

    /// <summary>Primeira porta oferecida a um servidor novo.</summary>
    public int GamePortRangeStart { get; init; } = GamePortDefaults.First;

    /// <summary>Última porta oferecida a um servidor novo (inclusive).</summary>
    public int GamePortRangeEnd { get; init; } = GamePortDefaults.Last;

    /// <summary>
    ///     Domínio ou DDNS desta máquina, sem porta. Vazio = limpar, e volta a
    ///     valer o IP detectado.
    /// </summary>
    public string? PublicHost { get; init; }

    /// <summary>
    ///     Client ID da app Azure do login com a Microsoft. Vazio = limpar
    ///     (não é segredo, então a tela sempre devolve o valor atual).
    /// </summary>
    public string? AzureClientId { get; init; }

    /// <summary>Nova chave. Vazio = manter a atual.</summary>
    public string? CurseForgeApiKey { get; init; }

    public bool ClearCurseForgeApiKey { get; init; }

    /// <summary>Novo token da Cloudflare. Vazio = manter o atual.</summary>
    public string? CloudflareApiToken { get; init; }

    public bool ClearCloudflareApiToken { get; init; }

    /// <summary>Id da zona na Cloudflare. Vazio = limpar.</summary>
    public string? CloudflareZoneId { get; init; }

    /// <summary>Domínio sob o qual os servidores ganham nome. Vazio = limpar.</summary>
    public string? DnsBaseDomain { get; init; }

    /// <summary>Rótulo do registro A mantido pelo TCMine. Vazio = o padrão ("mc").</summary>
    public string? DnsHostLabel { get; init; }
}
