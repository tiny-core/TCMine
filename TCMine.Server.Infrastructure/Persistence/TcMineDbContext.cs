using Microsoft.EntityFrameworkCore;
using TCMine.Server.Domain.Blobs;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class TcMineDbContext(DbContextOptions<TcMineDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Modpack> Modpacks => Set<Modpack>();
    public DbSet<ModpackVersion> ModpackVersions => Set<ModpackVersion>();
    public DbSet<ModpackFile> ModpackFiles => Set<ModpackFile>();
    public DbSet<GameServer> GameServers => Set<GameServer>();
    public DbSet<Blob> Blobs => Set<Blob>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Varre o assembly atrás de IEntityTypeConfiguration<T>. Sem isto,
        // toda classe de configuração nova precisaria ser registrada à mão
        // aqui — e esquecer uma significa o EF inferir o mapeamento sozinho,
        // silenciosamente e quase sempre errado.
        builder.ApplyConfigurationsFromAssembly(typeof(TcMineDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Sem isto, o EF trata string como texto ilimitado. No Postgres não
        // faz diferença prática, mas em índice é ruim e a intenção fica
        // implícita. Cada configuração pode sobrescrever quando precisar.
        builder.Properties<string>().HaveMaxLength(512);

        // DateTimeOffset é o padrão do projeto e o Postgres armazena como
        // timestamptz. Deixar explícito evita surpresa de fuso.
        builder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
    }
}