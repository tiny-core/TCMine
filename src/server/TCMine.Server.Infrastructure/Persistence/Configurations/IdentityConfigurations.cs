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

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.PasswordResetTokenHash).HasMaxLength(64).IsFixedLength();
        builder.Property(u => u.MicrosoftObjectId).HasMaxLength(64);
        builder.Property(u => u.MinecraftUuid).HasMaxLength(32);
        builder.Property(u => u.DisplayName).HasMaxLength(128).IsRequired();

        // Login local e ponte para a conta Microsoft: único por definição.
        builder.HasIndex(u => u.Email).IsUnique();

        // Chave natural de identidade do lado Microsoft: é por ela que
        // reconhecemos quem voltou. Filtrado porque contas só-locais têm NULL
        // aqui, e vários NULL quebrariam o índice único.
        builder.HasIndex(u => u.MicrosoftObjectId)
            .IsUnique()
            .HasFilter(null);

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
