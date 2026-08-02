using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence.Configurations;

internal sealed class NewsConfiguration : IEntityTypeConfiguration<News>
{
    public void Configure(EntityTypeBuilder<News> builder)
    {
        builder.ToTable("news");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200);
        // A convenção global limita strings a 512; o corpo de um post é maior.
        builder.Property(n => n.Body).HasMaxLength(20_000);

        builder.HasIndex(n => n.ModpackId);
    }
}
