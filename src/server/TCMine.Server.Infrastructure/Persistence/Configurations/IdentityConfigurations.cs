using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.MicrosoftObjectId).HasMaxLength(64).IsRequired();
        builder.Property(u => u.MinecraftUuid).HasMaxLength(32);
        builder.Property(u => u.DisplayName).HasMaxLength(128).IsRequired();

        // Chave natural de identidade: é por ela que reconhecemos quem
        // voltou. Precisa ser única, ou dois logins criariam dois usuários.
        builder.HasIndex(u => u.MicrosoftObjectId).IsUnique();

        // Filtrado porque alguns usuários não têm conta Minecraft vinculada, e
        // vários NULL num índice único quebrariam a restrição.
        builder.HasIndex(u => u.MinecraftUuid)
            .IsUnique()
            .HasFilter(null);
    }
}

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Um vínculo por usuário e servidor. Dois papéis para a mesma pessoa
        // no mesmo servidor tornaria a autorização indeterminada.
        builder.HasIndex(m => new { m.UserId, m.GameServerId }).IsUnique();

        // A pergunta mais frequente do sistema: qual o papel deste usuário
        // neste servidor? Roda em toda checagem de permissão.
        builder.HasIndex(m => m.GameServerId);
    }
}