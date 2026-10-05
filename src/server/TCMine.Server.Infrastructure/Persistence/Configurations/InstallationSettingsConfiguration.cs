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
    }
}
