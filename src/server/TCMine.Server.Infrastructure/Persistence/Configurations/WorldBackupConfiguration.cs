using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class WorldBackupConfiguration : IEntityTypeConfiguration<WorldBackup>
{
    public void Configure(EntityTypeBuilder<WorldBackup> builder)
    {
        builder.ToTable("world_backups");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.FileName).HasMaxLength(128).IsRequired();
        builder.Property(b => b.ModpackVersionLabel).HasMaxLength(64);
        builder.Property(b => b.Note).HasMaxLength(256);

        builder.Property(b => b.Reason).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Apagar o servidor leva os registros de backup junto. Os arquivos em
        // disco saem com a pasta da instância, no DeleteGameServer.
        builder.HasOne<GameServer>()
            .WithMany()
            .HasForeignKey(b => b.GameServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.GameServerId);
    }
}
