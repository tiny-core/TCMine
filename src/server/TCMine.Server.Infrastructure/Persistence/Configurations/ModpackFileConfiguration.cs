using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class ModpackFileConfiguration : IEntityTypeConfiguration<ModpackFile>
{
    public void Configure(EntityTypeBuilder<ModpackFile> builder)
    {
        builder.ToTable("modpack_files");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Path).HasMaxLength(512).IsRequired();

        // SHA-256 em hex tem exatamente 64 caracteres. Coluna de tamanho
        // fixo deixa o banco otimizar melhor.
        builder.Property(f => f.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();

        builder.Property(f => f.Side).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(f => f.Origin).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(f => f.OriginReference).HasMaxLength(256);

        // Mesmo caminho duas vezes na mesma versão seria ambíguo na hora de
        // materializar a instância.
        builder.HasIndex(f => new { f.ModpackVersionId, f.Path }).IsUnique();

        // Usado para descobrir quais blobs continuam em uso antes de
        // apagar algum.
        builder.HasIndex(f => f.Sha256);
    }
}
