using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

public sealed class ImportRequestConfiguration : IEntityTypeConfiguration<ImportRequest>
{
    public void Configure(EntityTypeBuilder<ImportRequest> builder)
    {
        builder.ToTable("import_requests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ProjectId).HasMaxLength(64).IsRequired();
        builder.Property(r => r.FileId).HasMaxLength(64);
        builder.Property(r => r.DisplayName).HasMaxLength(256).IsRequired();

        // Enums como string: mesmo motivo do resto do modelo — inserir um valor
        // no meio do enum não pode reescrever o significado das linhas gravadas.
        builder.Property(r => r.Origin).HasConversion<string>().HasMaxLength(32).IsRequired();

        // O mesmo pack não pode ter duas importações em curso: reimportar em
        // paralelo criaria dois modpacks disputando a mesma procedência, que é o
        // que o ExistsFromUpstreamAsync já recusa no caso de uso.
        builder.HasIndex(r => new { r.Origin, r.ProjectId }).IsUnique();
    }
}
