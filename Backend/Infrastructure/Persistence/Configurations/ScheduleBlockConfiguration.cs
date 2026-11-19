using Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ScheduleBlockConfiguration : IEntityTypeConfiguration<ScheduleBlock>
{
    public void Configure(EntityTypeBuilder<ScheduleBlock> builder)
    {
        builder.ToTable("schedule_blocks", table =>
        {
            table.HasCheckConstraint("ck_schedule_blocks_range_order", "ends_at > starts_at");
        });

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        // A null dentist means the block applies to the whole clinic.
        builder.HasOne<Dentist>()
            .WithMany()
            .HasForeignKey(b => b.DentistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(b => b.Reason).HasMaxLength(ScheduleBlock.ReasonMaxLength).IsRequired();

        builder.ComplexProperty(b => b.Range, range =>
        {
            range.Property(r => r.Start).HasColumnName("starts_at");
            range.Property(r => r.End).HasColumnName("ends_at");
        });
    }
}