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
