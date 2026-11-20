using Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ChairConfiguration : IEntityTypeConfiguration<Chair>
{
    public void Configure(EntityTypeBuilder<Chair> builder)
    {
        builder.ToTable("chairs");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(Chair.NameMaxLength).IsRequired();
        builder.Property(c => c.IsActive);

        builder.HasIndex(c => c.Name).IsUnique();
    }
}