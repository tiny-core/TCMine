using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class AccessRequestConfiguration : IEntityTypeConfiguration<AccessRequest>
{
    public void Configure(EntityTypeBuilder<AccessRequest> builder)
    {
        builder.ToTable("access_requests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Apagar o servidor leva os pedidos junto — não há nada a decidir sobre
        // acesso a um servidor que não existe mais.
        builder.HasOne<GameServer>()
            .WithMany()
            .HasForeignKey(r => r.GameServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // A pergunta mais frequente do módulo: "este jogador já tem um pedido
        // pendente para este servidor?" — roda em todo clique de "Pedir acesso".
        builder.HasIndex(r => new { r.UserId, r.GameServerId });

        // A tela "Pedidos" do admin filtra por um conjunto de servidores e só
        // quer os pendentes.
        builder.HasIndex(r => new { r.GameServerId, r.Status });
    }
}
