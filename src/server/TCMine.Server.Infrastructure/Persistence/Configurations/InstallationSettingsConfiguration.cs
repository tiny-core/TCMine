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
        builder.Property(s => s.MailServerDomain).HasMaxLength(253);
        builder.Property(s => s.DefaultLoader).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Textos cifrados: o tamanho cresce com o algoritmo, então folga.
        builder.Property(s => s.CurseForgeApiKeyEncrypted).HasMaxLength(1024);
        builder.Property(s => s.SmtpPasswordEncrypted).HasMaxLength(1024);

        builder.Property(s => s.SmtpHost).HasMaxLength(256);
        builder.Property(s => s.SmtpUser).HasMaxLength(256);
        builder.Property(s => s.SmtpFrom).HasMaxLength(256);

        // Propriedade computada: não vira coluna.
        builder.Ignore(s => s.HasSmtp);
    }
}
