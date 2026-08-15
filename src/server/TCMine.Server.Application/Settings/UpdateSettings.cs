using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Settings;

/// <summary>
///     Grava a configuração da instalação.
///     Segredos seguem a regra "vazio = manter": a UI nunca recebe o valor atual
///     de volta, então um campo em branco significa "não mexi nisso", e não
///     "apague". Para remover de fato existe a flag explícita de limpeza.
/// </summary>
public sealed class UpdateSettings(ISettingsRepository repository)
{
    public async Task<Result> HandleAsync(UpdateSettingsCommand command, CancellationToken ct)
    {
        if (command.DefaultMemoryMb is < 512)
            return Result.Fail("A RAM padrão precisa ser de pelo menos 512 MB.");

        if (command.WorldBackupKeepCount is < 0)
            return Result.Fail("A retenção de backups não pode ser negativa.");

        if (command.SmtpPort is < 1 or > 65535)
            return Result.Fail("Porta de SMTP inválida.");

        var settings = await repository.GetAsync(ct);

        settings.DefaultMinecraftVersion = string.IsNullOrWhiteSpace(command.DefaultMinecraftVersion)
            ? null
            : command.DefaultMinecraftVersion.Trim();
        settings.DefaultLoader = command.DefaultLoader;
        settings.DefaultMemoryMb = command.DefaultMemoryMb;
        settings.WorldBackupKeepCount = command.WorldBackupKeepCount;

        settings.SmtpHost = Trimmed(command.SmtpHost);
        settings.SmtpPort = command.SmtpPort;
        settings.SmtpUser = Trimmed(command.SmtpUser);
        settings.SmtpFrom = Trimmed(command.SmtpFrom);
        settings.SmtpUseTls = command.SmtpUseTls;

        // Os segredos vão em claro para o repositório, que cifra ao gravar.
        if (command.ClearCurseForgeApiKey)
            settings.CurseForgeApiKeyEncrypted = null;
        else if (!string.IsNullOrWhiteSpace(command.CurseForgeApiKey))
            settings.CurseForgeApiKeyEncrypted = command.CurseForgeApiKey.Trim();

        if (command.ClearSmtpPassword)
            settings.SmtpPasswordEncrypted = null;
        else if (!string.IsNullOrWhiteSpace(command.SmtpPassword))
            settings.SmtpPasswordEncrypted = command.SmtpPassword;

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

    /// <summary>Nova chave. Vazio = manter a atual.</summary>
    public string? CurseForgeApiKey { get; init; }

    public bool ClearCurseForgeApiKey { get; init; }

    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; } = 587;
    public string? SmtpUser { get; init; }

    /// <summary>Nova senha. Vazio = manter a atual.</summary>
    public string? SmtpPassword { get; init; }

    public bool ClearSmtpPassword { get; init; }
    public string? SmtpFrom { get; init; }
    public bool SmtpUseTls { get; init; } = true;
}
