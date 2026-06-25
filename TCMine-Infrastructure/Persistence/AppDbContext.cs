using Microsoft.EntityFrameworkCore;
using TCMine_Domain.Entities;

namespace TCMine_Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core com o conteúdo do servidor: novidades, modpacks, releases e
/// configs do jogador.
///
/// É <b>abstrato</b> de propósito: o EF Core mantém um único snapshot de modelo por
/// tipo de contexto, e migrations de SQLite e Postgres não são intercambiáveis (tipos
/// de coluna diferem). Por isso cada provider tem a sua subclasse concreta
/// (<see cref="SqliteAppDbContext"/> / <see cref="PostgresAppDbContext"/>),
/// cada uma com o seu próprio conjunto de migrations. Os serviços dependem desta base —
/// nunca da subclasse — e o DI resolve a concreta conforme o provider configurado.
/// </summary>
public abstract class AppDbContext(DbContextOptions options) : DbContext(options)
{
    // Ctor recebe as options não-genéricas; cada subclasse passa as suas DbContextOptions<T>

    public DbSet<NewsEntity> News => Set<NewsEntity>();
    public DbSet<ModpackEntity> Modpacks => Set<ModpackEntity>();

    // Arquivos de mod (um por FileId, compartilhados entre modpacks) + a junção N:N com os modpacks
    public DbSet<ModFileEntity> ModFiles => Set<ModFileEntity>();
    public DbSet<ModpackModEntity> ModpackMods => Set<ModpackModEntity>();
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

            // Apagar o modpack apaga os vínculos de mod e servidores em cascata
            e.HasMany(m => m.Mods)
                .WithOne(x => x.Modpack!)
                .HasForeignKey(x => x.ModpackId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(m => m.Servers)
                .WithOne(x => x.Modpack!)
                .HasForeignKey(x => x.ModpackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Arquivo de mod: PK natural = FileId (id do CF, ou negativo p/ uploads); nunca gerado pelo banco
        b.Entity<ModFileEntity>(e =>
        {
            e.HasKey(f => f.FileId);
            e.Property(f => f.FileId).ValueGeneratedNever();
        });

        // Junção modpack↔arquivo: PK composta + atributos por-modpack (Side/Target/ordem)
        b.Entity<ModpackModEntity>(e =>
        {
            e.HasKey(x => new { x.ModpackId, x.FileId });

            // Lado guardado como texto ("Both"/"Client"/"Server") — legível no banco
            e.Property(x => x.Side).HasConversion<string>().HasMaxLength(10);

            // Restrict: dropar um arquivo de um modpack não apaga o arquivo compartilhado (pode estar
            // em outros packs). Órfãos ficam no banco/cache, como já era a política dos jars.
            e.HasOne(x => x.ModFile)
                .WithMany(f => f.ModpackLinks)
                .HasForeignKey(x => x.FileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<NewsEntity>(e =>
        {
            // Notícia por modpack (FK opcional): null = global. Índice cobre o filtro do feed.
            e.HasIndex(n => n.ModpackId);
            // Apagar o modpack apaga as notícias dele; as globais (null) ficam intactas
            e.HasOne<ModpackEntity>()
                .WithMany()
                .HasForeignKey(n => n.ModpackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

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