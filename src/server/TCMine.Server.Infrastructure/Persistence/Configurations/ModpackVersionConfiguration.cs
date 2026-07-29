using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class ModpackVersionConfiguration : IEntityTypeConfiguration<ModpackVersion>
{
    public void Configure(EntityTypeBuilder<ModpackVersion> builder)
    {
        builder.ToTable("modpack_versions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Version).HasMaxLength(32).IsRequired();
        builder.Property(v => v.LoaderVersion).HasMaxLength(64).IsRequired();
        builder.Property(v => v.FailureReason).HasMaxLength(2048);

        builder.Ignore(v => v.IsPreRelease);

        // Enum como string na coluna.
        //
        // Como int, inserir um valor no meio do enum reescreveria o
        // significado de todas as linhas já gravadas — bug silencioso e
        // catastrófico. Como string, o custo é alguns bytes e o dump do
        // banco fica legível.
        builder.Property(v => v.State)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Duas versões com o mesmo número dentro do mesmo modpack não fazem
        // sentido: a versão é a identidade pública do pack.
        builder.HasIndex(v => new { v.ModpackId, v.Version }).IsUnique();

        builder.Metadata
            .FindNavigation(nameof(ModpackVersion.Files))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(v => v.Files)
            .WithOne()
            .HasForeignKey(f => f.ModpackVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}