using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class PlayerModpackSettingsConfiguration : IEntityTypeConfiguration<PlayerModpackSettings>
{
    public void Configure(EntityTypeBuilder<PlayerModpackSettings> builder)
    {
        builder.ToTable("player_modpack_settings");

        builder.HasKey(s => s.Id);

        // Sem HasMaxLength: as duas colunas são texto livre (text no Postgres),
        // e o teto vive no caso de uso — um varchar(n) curto demais é o tipo de
        // erro que o SQLite esconde e só aparece em produção (§9).
        builder.Property(s => s.KeyBindings).IsRequired();
        builder.Property(s => s.Options);

        builder.Ignore(s => s.LastSavedAt);

        // Uma linha por jogador e pack: é a pergunta que o launcher faz a cada
        // abertura do jogo, e duas linhas tornariam "a mais recente" ambígua.
        builder.HasIndex(s => new { s.UserId, s.ModpackId }).IsUnique();

        // O pack apagado leva as opções junto: não há o que aplicar a um pack
        // que não existe. A conta apagada também, pelo mesmo motivo — a fusão
        // de contas re-aponta antes de apagar (UserRepository.MergeAsync).
        builder.HasOne<Modpack>()
            .WithMany()
            .HasForeignKey(s => s.ModpackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
