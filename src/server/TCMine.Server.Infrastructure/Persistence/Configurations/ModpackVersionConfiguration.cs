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

        builder.Property(v => v.UpstreamFileId).HasMaxLength(64);
        builder.Property(v => v.UpstreamVersionLabel).HasMaxLength(128);

        // Mesma largura das outras URLs: link de CDN com assinatura passa de 512.
        builder.Property(v => v.UpstreamServerPackUrl).HasMaxLength(1024);

        // Sem limite: o snapshot guarda um par projeto/arquivo e o nome de CADA
        // mod do pack, então cresce com o tamanho dele — centenas de mods viram
        // dezenas de KB.
        //
        // SetMaxLength(null) e não um Property() pelado: chamar Property() sem
        // configurar nada NÃO desfaz a convenção global de 512 caracteres. Era
        // exatamente isso que estava aqui, com este mesmo comentário ao lado, e
        // por isso importar um pack grande morria com "value too long for type
        // character varying(512)" — uma coluna que a configuração dizia não ter
        // limite. Sem limite, o Npgsql emite "text" e o SQLite "TEXT".
        builder.Property(v => v.UpstreamSnapshotJson).Metadata.SetMaxLength(null);

        builder.Ignore(v => v.IsPreRelease);
        builder.Ignore(v => v.HasPendingMods);
        builder.Ignore(v => v.ManualUploads);
        builder.Ignore(v => v.HasManualUploads);

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

        builder.Metadata
            .FindNavigation(nameof(ModpackVersion.PendingMods))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(v => v.PendingMods)
            .WithOne()
            .HasForeignKey(p => p.ModpackVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
