using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using TCMine_Application.Abstractions;
using TCMine_Domain.Entities;

namespace TCMine_Server.Infrastructure.Server;

/// <summary>Snapshot imutável das settings (valores já descriptografados).</summary>
public sealed record ServerSettings(
    string? CfApiKey,
    string? AzureClientId,
    string? AzureTenantId,
    string? PublicBaseUrl);

/// <summary>
/// Lê e grava as configurações de runtime (token CurseForge, Azure client/tenant id)
/// na linha única <see cref="ServerSettingEntity"/>.
///
/// Singleton com cache em memória — a leitura é quente (o proxy CF consulta a cada
/// requisição). Como o <see cref="Persistence.AppDbContext"/> é scoped, abrimos um escopo curto via
/// <see cref="IServiceScopeFactory"/> para tocar o banco. Segredos são cifrados com
/// Data Protection antes de gravar e nunca trafegam em texto no banco.
///
/// Os valores são lidos exclusivamente do banco — sem fallback para variáveis de ambiente
/// (decisão de projeto: secrets configurados pelo painel, não por env). Antes de o Owner
/// preencher as settings, os getters devolvem null e os consumidores tratam como "não configurado".
/// </summary>
public sealed class ServerSettingsService(IServiceScopeFactory scopeFactory, IDataProtectionProvider protection)
{
    private readonly IDataProtector _protector = protection.CreateProtector("TCMine.ServerSettings.v1");

    // Serializa cargas/gravações concorrentes para o cache não correr
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Valores guardados no banco (sem fallback); null = ainda não carregado
    private ServerSettings? _cache;

    /// <summary>
    /// Disparado após uma gravação bem-sucedida, com o snapshot novo. Permite que a UI
    /// (ex.: o aviso de secrets pendentes no AdminLayout) reaja sem precisar de reload.
    /// </summary>
    public event Action<ServerSettings>? Changed;

    /// <summary>Valores guardados no banco (descriptografados) — usados pelo formulário admin.</summary>
    public async Task<ServerSettings> GetStoredAsync(CancellationToken ct = default)
    {
        if (_cache is not null) return _cache;

        await _gate.WaitAsync(ct);
        try
        {
            // Re-checa: outra chamada pode ter carregado enquanto esperávamos o lock
            return _cache ??= await LoadAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Grava (upsert), cifrando segredos, e atualiza o cache.</summary>
    public async Task SaveAsync(ServerSettings input, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IServerSettingsStore>();

            var row = await store.GetAsync(true, ct);
            if (row is null)
            {
                row = new ServerSettingEntity();
                store.Add(row);
            }

            row.CfApiKeyEncrypted = Protect(input.CfApiKey);
            row.AzureClientId = Normalize(input.AzureClientId);
            row.AzureTenantId = Normalize(input.AzureTenantId);
            row.PublicBaseUrl = Normalize(input.PublicBaseUrl);
            row.UpdatedAt = DateTime.UtcNow;

            await store.SaveChangesAsync(ct);

            // Atualiza o cache com os valores em texto (o que o formulário acabou de mandar)
            _cache = new ServerSettings(
                Normalize(input.CfApiKey), row.AzureClientId, row.AzureTenantId, row.PublicBaseUrl);
        }
        finally
        {
            _gate.Release();
        }

        // Notifica fora do lock para não segurar o gate enquanto os assinantes reagem
        Changed?.Invoke(_cache);
    }

    // ── Getters de conveniência (só o que está no banco) ─────────────────────────
    public async Task<string?> GetCfApiKeyAsync(CancellationToken ct = default)
    {
        return (await GetStoredAsync(ct)).CfApiKey;
    }

    public async Task<string?> GetAzureClientIdAsync(CancellationToken ct = default)
    {
        return (await GetStoredAsync(ct)).AzureClientId;
    }

    private async Task<ServerSettings> LoadAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IServerSettingsStore>();

        var row = await store.GetAsync(false, ct);

        return row is null
            ? new ServerSettings(null, null, null, null)
            : new ServerSettings(
                Unprotect(row.CfApiKeyEncrypted), row.AzureClientId, row.AzureTenantId, row.PublicBaseUrl);
    }

    private string? Protect(string? plain)
    {
        return string.IsNullOrWhiteSpace(plain) ? null : _protector.Protect(plain.Trim());
    }

    private string? Unprotect(string? cipher)
    {
        if (string.IsNullOrWhiteSpace(cipher)) return null;
        try
        {
            return _protector.Unprotect(cipher);
        }
        catch
        {
            // Chave de proteção rotacionada/ausente → trata como vazio em vez de quebrar
            return null;
        }
    }

    private static string? Normalize(string? s)
    {
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}