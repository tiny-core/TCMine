using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Blobs;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class BlobConfiguration : IEntityTypeConfiguration<Blob>
{
    public void Configure(EntityTypeBuilder<Blob> builder)
    {
        builder.ToTable("blobs");

        // A chave primária é o próprio hash. É o significado de
        // "endereçado por conteúdo": não existe identidade separada do
        // conteúdo, e a deduplicação vira consequência natural do modelo.
        builder.HasKey(b => b.Sha256);

        builder.Property(b => b.Sha256).HasMaxLength(64).IsFixedLength();
        builder.Property(b => b.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(b => b.StorageKey).HasMaxLength(512).IsRequired();

        builder.Property(b => b.Location)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
    }
}
