using Domain.Procedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ProcedureConfiguration : IEntityTypeConfiguration<Procedure>
{
    public void Configure(EntityTypeBuilder<Procedure> builder)
    {
        builder.ToTable("procedures", table =>
        {
            table.HasCheckConstraint("ck_procedures_duration_positive", "duration_minutes > 0");
            table.HasCheckConstraint("ck_procedures_price_not_negative", "base_price >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(Procedure.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Procedure.DescriptionMaxLength);
        builder.Property(p => p.DurationMinutes);
        builder.Property(p => p.BasePrice).HasPrecision(10, 2);
        builder.Property(p => p.IsActive);

        // Calculated from DurationMinutes, so it has no column.
        builder.Ignore(p => p.Duration);

        builder.HasIndex(p => p.Name).IsUnique();
    }
}