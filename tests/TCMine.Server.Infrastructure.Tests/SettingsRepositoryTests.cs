using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Settings;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     O que se grava nas configurações volta igual.
///     O <c>SaveAsync</c> copia campo a campo, e uma propriedade nova no domínio
///     que ninguém acrescenta à cópia não quebra nada: o valor só some. Já
///     aconteceu duas vezes — a retenção de backups voltava a 5 a cada "Salvar",
///     e a faixa de portas da 1.2.0 nunca chegou ao banco, com a tela dizendo
///     "salvo".
///     Por isso o teste não lista campos: percorre TODAS as propriedades da
///     entidade. Uma propriedade nova reprova aqui até ganhar um valor no
///     <see cref="Changed" /> — e, com o valor, reprova de novo se a cópia a
///     esquecer.
/// </summary>
public sealed class SettingsRepositoryTests : IDisposable
{
    private readonly SqliteTestFactory _factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IEnumerable<PropertyInfo> Properties =>
        typeof(InstallationSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanWrite);

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Tudo_o_que_se_grava_volta_igual()
    {
        var repo = new SettingsRepository(_factory, new EphemeralDataProtectionProvider());
        var changed = Changed();

        await repo.SaveAsync(changed, Ct);
        var reloaded = await repo.GetAsync(Ct);

        foreach (var property in Properties)
        {
            property.GetValue(reloaded).ShouldBe(
                property.GetValue(changed),
                $"{property.Name} não sobreviveu ao SaveAsync: falta a cópia dela em SettingsRepository");
        }
    }

    [Fact]
    public void O_teste_acima_cobre_todas_as_propriedades()
    {
        // Um valor igual ao padrão passaria no teste acima mesmo sem ser
        // gravado. Exigir que cada propriedade DIFIRA do padrão é o que obriga
        // quem cria uma propriedade a vir aqui.
        var changed = Changed();
        var defaults = new InstallationSettings();

        foreach (var property in Properties)
        {
            property.GetValue(changed).ShouldNotBe(
                property.GetValue(defaults),
                $"dê a {property.Name} um valor diferente do padrão em Changed()");
        }
    }

    [Fact]
    public async Task O_token_da_cloudflare_fica_cifrado_no_banco()
    {
        // O token edita a zona de DNS inteira. Um vazamento só do banco não pode
        // entregá-lo em claro.
        var repo = new SettingsRepository(_factory, new EphemeralDataProtectionProvider());

        await repo.SaveAsync(Changed(), Ct);

        await using var db = await _factory.CreateDbContextAsync(Ct);
        var stored = db.InstallationSettings.Single().CloudflareApiTokenEncrypted;

        stored.ShouldNotBeNullOrEmpty();
        stored!.ShouldNotContain("token-da-cloudflare");
    }

    private static InstallationSettings Changed() => new()
    {
        DefaultMinecraftVersion = "1.20.1",
        DefaultLoader = ModLoader.Fabric,
        DefaultMemoryMb = 6144,
        WorldBackupKeepCount = 9,
        GamePortRangeStart = 30000,
        GamePortRangeEnd = 30010,
        PublicHost = "jogar.exemplo.com",
        CloudflareApiTokenEncrypted = "token-da-cloudflare",
        CloudflareZoneId = "0123456789abcdef0123456789abcdef",
        DnsBaseDomain = "exemplo.com",
        DnsHostLabel = "jogo",
        CurseForgeApiKeyEncrypted = "chave-do-curseforge",
        AzureClientId = "55555555-5555-5555-5555-555555555555"
    };
}
