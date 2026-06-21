using Microsoft.EntityFrameworkCore;
using TCMine_Data.Entities;
using TCMine.Server.Data.Entities;

namespace TCMine_Data.Data;

/// <summary>
/// Contexto EF Core com o conteúdo do servidor: novidades, modpacks, releases e
/// configs do jogador.
///
/// É <b>abstrato</b> de propósito: o EF Core mantém um único snapshot de modelo por
/// tipo de contexto, e migrations de SQLite e Postgres não são intercambiáveis (tipos
/// de coluna diferem). Por isso cada provider tem a sua subclasse concreta
/// (<see cref="Providers.SqliteAppDbContext"/> / <see cref="Providers.PostgresAppDbContext"/>),
/// cada uma com o seu próprio conjunto de migrations. Os serviços dependem desta base —
/// nunca da subclasse — e o DI resolve a concreta conforme o provider configurado.
/// </summary>
public abstract class AppDbContext(DbContextOptions options) : DbContext(options)
{
    // Ctor recebe as options não-genéricas; cada subclasse passa as suas DbContextOptions<T>

    public DbSet<NewsEntity> News => Set<NewsEntity>();
    public DbSet<ModpackEntity> Modpacks => Set<ModpackEntity>();
    public DbSet<ModEntryEntity> Mods => Set<ModEntryEntity>();
    public DbSet<ServerEntryEntity> Servers => Set<ServerEntryEntity>();
    public DbSet<ReleaseEntity> Releases => Set<ReleaseEntity>();
    public DbSet<PlayerConfigEntity> PlayerConfigs => Set<PlayerConfigEntity>();
    public DbSet<ServerSettingEntity> Settings => Set<ServerSettingEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<ServerInstanceEntity> ServerInstances => Set<ServerInstanceEntity>();
    public DbSet<OverrideHistoryEntry> OverrideHistory => Set<OverrideHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserEntity>(e =>
        {
            // Username único (login). Normalizamos para minúsculas antes de gravar.
            e.HasIndex(u => u.Username).IsUnique();
            // Papel guardado como texto ("Owner", "Admin", …) em vez de int — legível no banco
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        // Configs do jogador: chave composta (Uuid, ModpackId)
        b.Entity<PlayerConfigEntity>().HasKey(p => new { p.Uuid, p.ModpackId });

        // Settings é uma linha única; a chave não é gerada pelo banco (controlamos o ID)
        b.Entity<ServerSettingEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).ValueGeneratedNever();
        });

        b.Entity<ModpackEntity>(e =>
        {
            e.HasKey(m => m.Id);

            // Loader guardado como texto ("NeoForge"/"Forge"/"Fabric"/"Quilt") — legível no banco
            e.Property(m => m.Loader).HasConversion<string>().HasMaxLength(20);

            // Apagar o modpack apaga os seus mods e servidores em cascata
            e.HasMany(m => m.Mods)
                .WithOne(x => x.Modpack!)
                .HasForeignKey(x => x.ModpackId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(m => m.Servers)
                .WithOne(x => x.Modpack!)
                .HasForeignKey(x => x.ModpackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Lado do mod guardado como texto ("Both"/"Client"/"Server") — legível no banco
        b.Entity<ModEntryEntity>()
            .Property(x => x.Side).HasConversion<string>().HasMaxLength(10);

        b.Entity<OverrideHistoryEntry>(e =>
        {
            e.HasKey(h => h.Id);
            // Operação guardada como texto ("Edit"/"MoveFile"/…) — legível no banco
            e.Property(h => h.Operation).HasConversion<string>().HasMaxLength(20);
            // Índice por modpack + data: a pilha de desfazer pega a entrada mais recente do slug
            e.HasIndex(h => new { h.ModpackId, h.CreatedAt });
        });

        b.Entity<ServerInstanceEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

            // Restrict: não deixa apagar um modpack que ainda tem servidores derivados dele
            // (evita órfãos e remoção acidental de um modpack com servidor ativo).
            e.HasOne(s => s.Modpack)
                .WithMany()
                .HasForeignKey(s => s.ModpackId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}