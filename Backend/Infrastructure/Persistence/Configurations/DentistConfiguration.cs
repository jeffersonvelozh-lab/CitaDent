using Domain.Scheduling;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class DentistConfiguration : IEntityTypeConfiguration<Dentist>
{
    public void Configure(EntityTypeBuilder<Dentist> builder)
    {
        builder.ToTable("dentists");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        // Each dentist signs in with exactly one user account; users are deactivated, never deleted.
        builder.HasOne<User>().WithOne().HasForeignKey<Dentist>(d => d.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(d => d.FirstName).HasMaxLength(Dentist.NameMaxLength).IsRequired();
        builder.Property(d => d.LastName).HasMaxLength(Dentist.NameMaxLength).IsRequired();
        builder.Property(d => d.Specialty).HasMaxLength(Dentist.SpecialtyMaxLength);
        builder.Property(d => d.LicenseNumber).HasMaxLength(Dentist.LicenseNumberMaxLength).IsRequired();
        builder.Property(d => d.IsActive);

        builder.HasIndex(d => d.UserId).IsUnique();

        // Backstop for the license check in Application, which also ignores case.
        builder.HasIndex(d => d.LicenseNumber).IsUnique();

        // The weekly schedule lives in its own table and is saved and loaded together with the dentist.
        builder.OwnsMany(d => d.WeeklySchedule, hours =>
        {
            hours.ToTable("dentist_schedules", table =>
            {
                table.HasCheckConstraint("ck_dentist_schedules_hours_order", "end_time > start_time");
                table.HasCheckConstraint("ck_dentist_schedules_day_range", "day_of_week between 0 and 6");
            });

            hours.WithOwner().HasForeignKey("DentistId");

            hours.Property<int>("Id");
            hours.HasKey("Id");

            hours.Property(h => h.Day).HasColumnName("day_of_week").HasConversion<short>();
            hours.Property(h => h.Start).HasColumnName("start_time");
            hours.Property(h => h.End).HasColumnName("end_time");
        });

        builder.Navigation(d => d.WeeklySchedule).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}