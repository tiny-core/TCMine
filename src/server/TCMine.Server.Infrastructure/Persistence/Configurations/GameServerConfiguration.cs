using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class GameServerConfiguration : IEntityTypeConfiguration<GameServer>
{
    public void Configure(EntityTypeBuilder<GameServer> builder)
    {
        builder.ToTable("game_servers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.WhitelistEnabled).IsRequired();

        // HasWorld é computado (deriva de WorldInitializedAt) — não é coluna.
        builder.Ignore(s => s.HasWorld);

        builder.Property(s => s.Name).HasMaxLength(128).IsRequired();
        builder.Property(s => s.ConnectAddress).HasMaxLength(256).IsRequired();
        builder.Property(s => s.ContainerId).HasMaxLength(128);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Marcado como sensível para o EF nunca imprimir o valor em log de
        // parâmetro. Quem tem a senha do RCON tem controle total da máquina
        // do jogo, e log costuma ir parar em lugar menos protegido que o banco.
        builder.Property(s => s.RconSecret)
            .HasMaxLength(128)
            .IsRequired()
            .HasSentinel(string.Empty);

        // Único: é o banco, e não só a validação, que garante que dois
        // servidores nunca disputam a mesma porta do host.
        builder.HasIndex(s => s.GamePort).IsUnique();

        builder.HasIndex(s => s.OwnerId);
        builder.HasIndex(s => s.ModpackVersionId);
        builder.HasIndex(s => s.ModpackId);

        // Apagar a nuvem desliga a nuvem dos servidores, não apaga os servidores.
        builder.HasOne<CloudVault>()
            .WithMany()
            .HasForeignKey(s => s.CloudVaultId)
            .OnDelete(DeleteBehavior.SetNull);

        // Sem navegação para Modpack de propósito: GameServer e Modpack são
        // agregados diferentes. Referência por Id evita que uma consulta de
        // servidor arraste o pack inteiro com todos os arquivos junto.
    }
}
