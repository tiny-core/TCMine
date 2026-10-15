using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class InstallationSettingsConfiguration : IEntityTypeConfiguration<InstallationSettings>
{
    public void Configure(EntityTypeBuilder<InstallationSettings> builder)
    {
        builder.ToTable("installation_settings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.DefaultMinecraftVersion).HasMaxLength(32);
        builder.Property(s => s.DefaultLoader).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Texto cifrado: o tamanho cresce com o algoritmo, então folga.
        builder.Property(s => s.CurseForgeApiKeyEncrypted).HasMaxLength(1024);

        // GUID em texto tem 36 caracteres; a folga cobre um id entre chaves,
        // que é como o portal do Azure às vezes o entrega ao copiar.
        builder.Property(s => s.AzureClientId).HasMaxLength(64);

        // Um nome DNS completo tem no máximo 255 caracteres (o mesmo número
        // está em UpdateSettings.PublicHostMaxLength, que é quem recusa).
        builder.Property(s => s.PublicHost).HasMaxLength(255);
        builder.Property(s => s.LanHost).HasMaxLength(255);

        // DNS (Cloudflare). O token é texto cifrado, com a mesma folga da chave
        // do CurseForge; o Zone ID tem 32 caracteres; o domínio e o rótulo
        // seguem os limites do DNS (255 e 63).
        builder.Property(s => s.CloudflareApiTokenEncrypted).HasMaxLength(1024);
        builder.Property(s => s.CloudflareZoneId).HasMaxLength(64);
        builder.Property(s => s.DnsBaseDomain).HasMaxLength(255);
        builder.Property(s => s.DnsHostLabel).HasMaxLength(63);

        // Nulo de propósito: instalação de antes da escolha deduz o modo do que
        // está preenchido (InstallationSettings.EffectiveNetworkMode), e a
        // migration não precisa adivinhar nada.
        builder.Property(s => s.NetworkMode).HasConversion<string>().HasMaxLength(32);

        // Calculados a partir das colunas acima — não são colunas.
        builder.Ignore(s => s.HasCloudflareCredentials);
        builder.Ignore(s => s.EffectiveNetworkMode);
        builder.Ignore(s => s.EffectiveCloudflareUsesPublicHost);
        builder.Ignore(s => s.EffectivePublicHost);
        builder.Ignore(s => s.DnsEnabled);
        builder.Ignore(s => s.ManagedDnsHost);
        builder.Ignore(s => s.DnsSyncInterval);
        builder.Ignore(s => s.NeedsPublicIpDetection);
        builder.Ignore(s => s.PublicIpDetectionEnabled);
        builder.Ignore(s => s.TunnelEnabled);
        builder.Ignore(s => s.TunnelOrigin);

        // Túnel: o Account ID tem 32 caracteres e o do túnel é um GUID (36); a
        // folga é a mesma do Zone ID. O host de origem segue o limite de DNS.
        builder.Property(s => s.CloudflareAccountId).HasMaxLength(64);
        builder.Property(s => s.CloudflareTunnelId).HasMaxLength(64);
        builder.Property(s => s.TunnelOriginHost).HasMaxLength(255);

        // Padrão no banco pelo mesmo motivo da faixa de portas: a linha já
        // existe, e sem ele a migration a deixaria com intervalo zero.
        builder.Property(s => s.DnsSyncIntervalMinutes).HasDefaultValue(5);

        // Com padrão NO BANCO: a linha de configurações já existe nas
        // instalações em uso, e sem isto a migration a deixaria com a faixa
        // 0–0. O valor é o literal, e não a constante do domínio, porque
        // migration antiga não pode mudar quando uma constante mudar.
        builder.Property(s => s.GamePortRangeStart).HasDefaultValue(25565);
        builder.Property(s => s.GamePortRangeEnd).HasDefaultValue(25599);
    }
}
