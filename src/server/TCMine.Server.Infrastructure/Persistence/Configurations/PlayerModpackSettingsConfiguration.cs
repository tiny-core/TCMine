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

        // Texto livre (text no Postgres): o teto vive no caso de uso. O limite
        // tem de ser DESFEITO, e não só omitido — a convenção global dá 512 a
        // toda string, e Property() pelado o herda. Foi o que aconteceu aqui:
        // o comentário dizia "sem limite", a coluna saiu varchar(512), e as
        // teclas de qualquer pack de verdade (KB de linhas key_*) estouravam
        // no Postgres a cada gravação, enquanto o SQLite dos testes aceitava.
        builder.Property(s => s.KeyBindings).IsRequired().Metadata.SetMaxLength(null);
        builder.Property(s => s.Options).Metadata.SetMaxLength(null);

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
