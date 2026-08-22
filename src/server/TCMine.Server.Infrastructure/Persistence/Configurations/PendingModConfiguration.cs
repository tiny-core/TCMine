using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class PendingModConfiguration : IEntityTypeConfiguration<PendingMod>
{
    public void Configure(EntityTypeBuilder<PendingMod> builder)
    {
        builder.ToTable("pending_mods");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProjectSlug).HasMaxLength(128).IsRequired();
        builder.Property(p => p.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(p => p.FileId).HasMaxLength(64);
        // Detail carrega a mensagem de erro da origem, que não temos como
        // limitar: 2048 é o mesmo teto do FailureReason da versão. Uma coluna
        // curta aqui derruba a ingestão na hora de registrar POR QUE um mod
        // falhou — o diagnóstico quebrando a operação que ele deveria explicar.
        builder.Property(p => p.Detail).HasMaxLength(2048);

        // URL de CDN com assinatura passa de 512 com facilidade; mesmo teto do
        // IconUrl, pelo mesmo motivo.
        builder.Property(p => p.PageUrl).HasMaxLength(1024);

        // Enums como string: mesmo motivo do resto do modelo — inserir um valor
        // no meio do enum não pode reescrever o significado das linhas gravadas.
        builder.Property(p => p.Origin).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.Reason).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.Side).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Um mod não pode ficar pendente duas vezes na mesma versão.
        builder.HasIndex(p => new { p.ModpackVersionId, p.ProjectSlug }).IsUnique();
    }
}
