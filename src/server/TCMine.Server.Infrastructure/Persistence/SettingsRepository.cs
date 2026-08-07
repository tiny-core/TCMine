using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Infrastructure.Persistence;

/// <summary>
///     Configuração da instalação, com os segredos cifrados em repouso.
///     Usa a Data Protection do ASP.NET (chaves gerenciadas pela plataforma), em
///     vez de criptografia própria: se o banco vazar sozinho, a chave da API e a
///     senha de SMTP não vão junto em claro.
/// </summary>
public sealed class SettingsRepository : ISettingsRepository
{
    private readonly IDbContextFactory<TcMineDbContext> _factory;
    private readonly IDataProtector _protector;

    public SettingsRepository(IDbContextFactory<TcMineDbContext> factory, IDataProtectionProvider protection)
    {
        _factory = factory;

        // O "purpose" isola este uso: um texto cifrado aqui não pode ser
        // decifrado por outro protetor da aplicação.
        _protector = protection.CreateProtector("TCMine.Settings.v1");
    }

    public async Task<InstallationSettings> GetAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var settings = await db.InstallationSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        if (settings is null)
        {
            // Primeira leitura numa instalação nova: cria a linha com os padrões.
            settings = new InstallationSettings();
            db.InstallationSettings.Add(settings);
            await db.SaveChangesAsync(ct);
        }

        // Devolve em claro para quem for usar; a UI decide o que exibir.
        settings.CurseForgeApiKeyEncrypted = Unprotect(settings.CurseForgeApiKeyEncrypted);
        settings.SmtpPasswordEncrypted = Unprotect(settings.SmtpPasswordEncrypted);
        return settings;
    }

    public async Task SaveAsync(InstallationSettings settings, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var stored = await db.InstallationSettings.FirstOrDefaultAsync(ct);
        if (stored is null)
        {
            stored = new InstallationSettings();
            db.InstallationSettings.Add(stored);
        }

        stored.DefaultMinecraftVersion = settings.DefaultMinecraftVersion;
        stored.DefaultLoader = settings.DefaultLoader;
        stored.DefaultMemoryMb = settings.DefaultMemoryMb;

        stored.SmtpHost = settings.SmtpHost;
        stored.SmtpPort = settings.SmtpPort;
        stored.SmtpUser = settings.SmtpUser;
        stored.SmtpFrom = settings.SmtpFrom;
        stored.SmtpUseTls = settings.SmtpUseTls;

        stored.CurseForgeApiKeyEncrypted = Protect(settings.CurseForgeApiKeyEncrypted);
        stored.SmtpPasswordEncrypted = Protect(settings.SmtpPasswordEncrypted);

        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetCurseForgeApiKeyAsync(CancellationToken ct)
    {
        var settings = await GetAsync(ct);
        return settings.CurseForgeApiKeyEncrypted;
    }

    public async Task<string?> GetSmtpPasswordAsync(CancellationToken ct)
    {
        var settings = await GetAsync(ct);
        return settings.SmtpPasswordEncrypted;
    }

    private string? Protect(string? plaintext) =>
        string.IsNullOrEmpty(plaintext) ? null : _protector.Protect(plaintext);

    private string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return null;

        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Chaves de proteção trocadas (máquina nova, keyring perdido): o
            // valor virou lixo. Tratar como "não configurado" é melhor do que
            // derrubar a página de configurações — o admin regrava o segredo.
            return null;
        }
    }
}
