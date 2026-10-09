using Microsoft.EntityFrameworkCore;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Common;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class TcMineDbContext(DbContextOptions<TcMineDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<ModpackMembership> ModpackMemberships => Set<ModpackMembership>();
    public DbSet<Modpack> Modpacks => Set<Modpack>();
    public DbSet<ModpackVersion> ModpackVersions => Set<ModpackVersion>();
    public DbSet<ModpackFile> ModpackFiles => Set<ModpackFile>();
    public DbSet<PendingMod> PendingMods => Set<PendingMod>();
    public DbSet<ImportRequest> ImportRequests => Set<ImportRequest>();
    public DbSet<GameServer> GameServers => Set<GameServer>();
    public DbSet<WorldBackup> WorldBackups => Set<WorldBackup>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();

    public DbSet<News> News => Set<News>();

    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();

    public DbSet<InstallationSettings> InstallationSettings => Set<InstallationSettings>();

    // Nuvem de itens (mod tccloud) — docs/CLOUD-STORAGE.md.
    public DbSet<CloudVault> CloudVaults => Set<CloudVault>();
    public DbSet<CloudServerCredential> CloudServerCredentials => Set<CloudServerCredential>();
    public DbSet<CloudChannel> CloudChannels => Set<CloudChannel>();
    public DbSet<CloudItemType> CloudItemTypes => Set<CloudItemType>();
    public DbSet<CloudBalance> CloudBalances => Set<CloudBalance>();
    public DbSet<CloudLease> CloudLeases => Set<CloudLease>();
    public DbSet<CloudBatch> CloudBatches => Set<CloudBatch>();
    public DbSet<CloudLedgerEntry> CloudLedger => Set<CloudLedgerEntry>();
    public DbSet<CloudQuarantine> CloudQuarantine => Set<CloudQuarantine>();
    public DbSet<CloudItemRule> CloudItemRules => Set<CloudItemRule>();
    public DbSet<CloudSuspectItem> CloudSuspectItems => Set<CloudSuspectItem>();
    public DbSet<CloudRollbackIncident> CloudRollbackIncidents => Set<CloudRollbackIncident>();
    public DbSet<CloudDoubtfulOperation> CloudDoubtfulOperations => Set<CloudDoubtfulOperation>();
    public DbSet<CloudAdminAuditEntry> CloudAdminAudit => Set<CloudAdminAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Varre o assembly atrás de IEntityTypeConfiguration<T>. Sem isto,
        // toda classe de configuração nova precisaria ser registrada à mão
        // aqui — e esquecer uma significa o EF inferir o mapeamento sozinho,
        // silenciosamente e quase sempre errado.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TcMineDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Sem isto, o EF trata string como texto ilimitado. No Postgres não
        // faz diferença prática, mas em índice é ruim e a intenção fica
        // implícita. Cada configuração pode sobrescrever quando precisar.
        configurationBuilder.Properties<string>().HaveMaxLength(512);

        // O tipo de coluna de data fica a cargo de cada provider: o Npgsql
        // mapeia DateTimeOffset para timestamptz por conta própria, e o
        // SQLite guarda como texto. Fixar aqui quebraria um dos dois.
    }
}
