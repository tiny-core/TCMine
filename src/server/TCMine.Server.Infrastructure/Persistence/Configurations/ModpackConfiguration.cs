using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class ModpackConfiguration : IEntityTypeConfiguration<Modpack>
{
    public void Configure(EntityTypeBuilder<Modpack> builder)
    {
        builder.ToTable("modpacks");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Slug).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Name).HasMaxLength(128).IsRequired();
        builder.Property(m => m.Summary).HasMaxLength(1024);
        builder.Property(m => m.IconBlobSha256).HasMaxLength(64);
        builder.Property(m => m.MinecraftVersion).HasMaxLength(32).IsRequired();
        builder.Property(m => m.Loader).HasConversion<string>().HasMaxLength(32).IsRequired();

        // O slug vai na URL, então precisa ser único. Deixar essa garantia
        // só na validação da aplicação não basta: duas requisições
        // simultâneas passariam pela checagem antes de qualquer uma gravar.
        builder.HasIndex(m => m.Slug).IsUnique();

        builder.HasIndex(m => m.OwnerId);

        // A coleção é exposta como List<T> somente-leitura na entidade; o EF
        // precisa saber que pode escrever nela pelo campo de apoio.
        builder.Metadata
            .FindNavigation(nameof(Modpack.Versions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(m => m.Versions)
            .WithOne()
            .HasForeignKey(v => v.ModpackId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
