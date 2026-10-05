using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

// Tabelas da nuvem de itens (mod tccloud). Num arquivo só, como as de
// identidade: são um módulo, e quem mexe numa quase sempre precisa ver as
// outras (as chaves estrangeiras se cruzam).
//
// Regra geral das FKs: Restrict. Dado de saldo e histórico não pode sumir em
// cascata por um clique em "apagar" — perder o ledger é perder a única forma
// de investigar uma duplicação. A exceção é a chave do servidor, que não vale
// nada sem o servidor.

public sealed class CloudVaultConfiguration : IEntityTypeConfiguration<CloudVault>
{
    public void Configure(EntityTypeBuilder<CloudVault> builder)
    {
        builder.ToTable("cloud_vaults");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).HasMaxLength(CloudVault.NameMaxLength).IsRequired();
        builder.Property(v => v.PolicyMode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Ignore(v => v.LeaseTtl);
        builder.HasIndex(v => v.OwnerId);
    }
}

public sealed class CloudServerCredentialConfiguration : IEntityTypeConfiguration<CloudServerCredential>
{
    public void Configure(EntityTypeBuilder<CloudServerCredential> builder)
    {
        builder.ToTable("cloud_server_credentials");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.KeyPrefix).HasMaxLength(CloudServerCredential.PrefixLength).IsRequired();
        builder.Property(c => c.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.ModVersion).HasMaxLength(32);
        builder.Ignore(c => c.IsActive);

        // Toda requisição do mod começa por aqui: achar a chave pelo prefixo.
        builder.HasIndex(c => c.KeyPrefix).IsUnique();
        builder.HasIndex(c => c.GameServerId);

        builder.HasOne<GameServer>().WithMany().HasForeignKey(c => c.GameServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CloudVault>().WithMany().HasForeignKey(c => c.VaultId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudChannelConfiguration : IEntityTypeConfiguration<CloudChannel>
{
    public void Configure(EntityTypeBuilder<CloudChannel> builder)
    {
        builder.ToTable("cloud_channels");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.PlayerUuid).HasMaxLength(32).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(CloudChannel.NameMaxLength).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.FrozenReason).HasMaxLength(256);
        builder.Ignore(c => c.IsFrozen);

        // Os canais de um jogador numa nuvem: a consulta do acquire. O nome é
        // único dentro dela — dois "Principal" confundiriam o jogador.
        builder.HasIndex(c => new { c.VaultId, c.PlayerUuid, c.Name }).IsUnique();

        builder.HasOne<CloudVault>().WithMany().HasForeignKey(c => c.VaultId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudItemTypeConfiguration : IEntityTypeConfiguration<CloudItemType>
{
    public void Configure(EntityTypeBuilder<CloudItemType> builder)
    {
        builder.ToTable("cloud_item_types");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Fingerprint).HasMaxLength(CloudItemType.FingerprintLength).IsRequired();
        builder.Property(i => i.ItemId).HasMaxLength(256).IsRequired();
        builder.Property(i => i.ModId).HasMaxLength(128).IsRequired();
        builder.Property(i => i.DisplayName).HasMaxLength(128).IsRequired();
        builder.Property(i => i.Encoded).IsRequired();

        builder.HasIndex(i => i.Fingerprint).IsUnique();
    }
}

public sealed class CloudBalanceConfiguration : IEntityTypeConfiguration<CloudBalance>
{
    public void Configure(EntityTypeBuilder<CloudBalance> builder)
    {
        builder.ToTable("cloud_balances");
        builder.HasKey(b => b.Id);
        builder.HasIndex(b => new { b.ChannelId, b.ItemTypeId }).IsUnique();

        builder.HasOne<CloudChannel>().WithMany().HasForeignKey(b => b.ChannelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CloudItemType>().WithMany().HasForeignKey(b => b.ItemTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudLeaseConfiguration : IEntityTypeConfiguration<CloudLease>
{
    public void Configure(EntityTypeBuilder<CloudLease> builder)
    {
        builder.ToTable("cloud_leases");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.PlayerUuid).HasMaxLength(32).IsRequired();

        // Token de concorrência: o UPDATE leva "WHERE Version = <lido>". Dois
        // lotes do mesmo jogador ao mesmo tempo, um perde e é reenviado.
        builder.Property(l => l.Version).IsConcurrencyToken();

        builder.HasIndex(l => new { l.VaultId, l.PlayerUuid }).IsUnique();
        builder.HasIndex(l => l.HolderServerId);

        builder.HasOne<CloudVault>().WithMany().HasForeignKey(l => l.VaultId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudBatchConfiguration : IEntityTypeConfiguration<CloudBatch>
{
    public void Configure(EntityTypeBuilder<CloudBatch> builder)
    {
        builder.ToTable("cloud_batches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.PlayerUuid).HasMaxLength(32).IsRequired();
        builder.Property(b => b.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        // O mesmo lote nunca entra duas vezes, nem com duas requisições juntas.
        builder.HasIndex(b => new { b.VaultId, b.PlayerUuid, b.Epoch, b.Seq }).IsUnique();
        builder.HasIndex(b => new { b.ServerId, b.Epoch, b.Seq });

        builder.HasOne<CloudVault>().WithMany().HasForeignKey(b => b.VaultId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudLedgerEntryConfiguration : IEntityTypeConfiguration<CloudLedgerEntry>
{
    public void Configure(EntityTypeBuilder<CloudLedgerEntry> builder)
    {
        builder.ToTable("cloud_ledger");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(512);

        // Histórico de um canal, em ordem (Id é GUID v7: cronológico).
        builder.HasIndex(e => new { e.ChannelId, e.Id });
        builder.HasIndex(e => e.BatchId);

        builder.HasOne<CloudChannel>().WithMany().HasForeignKey(e => e.ChannelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CloudItemType>().WithMany().HasForeignKey(e => e.ItemTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CloudBatch>().WithMany().HasForeignKey(e => e.BatchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudQuarantineConfiguration : IEntityTypeConfiguration<CloudQuarantine>
{
    public void Configure(EntityTypeBuilder<CloudQuarantine> builder)
    {
        builder.ToTable("cloud_quarantine");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Reason).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(q => q.Detail).HasMaxLength(1024).IsRequired();
        builder.Property(q => q.Resolution).HasConversion<string>().HasMaxLength(16);
        // O lote inteiro: cresce com o número de operações, então sem limite
        // (Property() pelado não desfaz a convenção de 512 — ver
        // ColumnLengthConventionTests).
        builder.Property(q => q.PayloadJson).IsRequired().Metadata.SetMaxLength(null);
        builder.Ignore(q => q.IsOpen);

        builder.HasIndex(q => new { q.VaultId, q.ResolvedAt });

        builder.HasOne<CloudVault>().WithMany().HasForeignKey(q => q.VaultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CloudBatch>().WithMany().HasForeignKey(q => q.BatchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudItemRuleConfiguration : IEntityTypeConfiguration<CloudItemRule>
{
    public void Configure(EntityTypeBuilder<CloudItemRule> builder)
    {
        builder.ToTable("cloud_item_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Scope).HasConversion<string>().HasMaxLength(8).IsRequired();
        builder.Property(r => r.Action).HasConversion<string>().HasMaxLength(8).IsRequired();
        builder.Property(r => r.Pattern).HasMaxLength(CloudItemRule.PatternMaxLength).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(256);

        // Uma regra por (escopo, padrão): duas regras para o mesmo alvo seriam
        // uma briga que só a precedência resolveria, sem o dono enxergar.
        builder.HasIndex(r => new { r.VaultId, r.Scope, r.Pattern }).IsUnique();

        builder.HasOne<CloudVault>().WithMany().HasForeignKey(r => r.VaultId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudSuspectItemConfiguration : IEntityTypeConfiguration<CloudSuspectItem>
{
    public void Configure(EntityTypeBuilder<CloudSuspectItem> builder)
    {
        builder.ToTable("cloud_suspect_items");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.ItemId).HasMaxLength(256).IsRequired();
        builder.Property(s => s.Evidence).HasMaxLength(256).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(s => new { s.VaultId, s.ItemId }).IsUnique();
        builder.HasOne<CloudVault>().WithMany().HasForeignKey(s => s.VaultId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudRollbackIncidentConfiguration : IEntityTypeConfiguration<CloudRollbackIncident>
{
    public void Configure(EntityTypeBuilder<CloudRollbackIncident> builder)
    {
        builder.ToTable("cloud_rollback_incidents");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.CheckpointJson).IsRequired().Metadata.SetMaxLength(null);
        builder.Property(i => i.Detail).HasMaxLength(2048).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Ignore(i => i.IsOpen);
        builder.HasIndex(i => new { i.ServerId, i.Status });
        builder.HasIndex(i => new { i.VaultId, i.Status });
        builder.HasOne<CloudVault>().WithMany().HasForeignKey(i => i.VaultId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudDoubtfulOperationConfiguration : IEntityTypeConfiguration<CloudDoubtfulOperation>
{
    public void Configure(EntityTypeBuilder<CloudDoubtfulOperation> builder)
    {
        builder.ToTable("cloud_doubtful_operations");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.ReportId).HasMaxLength(64).IsRequired();
        builder.Property(d => d.PlayerUuid).HasMaxLength(32).IsRequired();
        builder.Property(d => d.Fingerprint).HasMaxLength(CloudItemType.FingerprintLength).IsRequired();
        builder.Property(d => d.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Ignore(d => d.IsOpen);

        // O mesmo relatório reenviado não duplica a fila.
        builder.HasIndex(d => new { d.ServerId, d.ReportId, d.Index }).IsUnique();
        builder.HasIndex(d => new { d.VaultId, d.Status });

        builder.HasOne<CloudVault>().WithMany().HasForeignKey(d => d.VaultId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CloudAdminAuditEntryConfiguration : IEntityTypeConfiguration<CloudAdminAuditEntry>
{
    public void Configure(EntityTypeBuilder<CloudAdminAuditEntry> builder)
    {
        builder.ToTable("cloud_admin_audit");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Details).HasMaxLength(2048).IsRequired();
        builder.HasIndex(a => new { a.VaultId, a.Id });
        builder.HasOne<CloudVault>().WithMany().HasForeignKey(a => a.VaultId).OnDelete(DeleteBehavior.Restrict);
    }
}
