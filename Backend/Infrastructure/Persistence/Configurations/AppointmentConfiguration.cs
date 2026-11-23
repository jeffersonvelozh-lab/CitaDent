using Domain.Appointments;
using Domain.Patients;
using Domain.Procedures;
using Domain.Scheduling;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments", table =>
        {
            table.HasCheckConstraint("ck_appointments_range_order", "ends_at > starts_at");
            table.HasCheckConstraint("ck_appointments_status", "status in (1, 2, 3, 4)");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.HasOne<Patient>().WithMany().HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Dentist>().WithMany().HasForeignKey(a => a.DentistId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Chair>().WithMany().HasForeignKey(a => a.ChairId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Procedure>().WithMany().HasForeignKey(a => a.ProcedureId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Status).HasConversion<short>();
        builder.Property(a => a.CancelReason).HasMaxLength(Appointment.CancelReasonMaxLength);

        // The user who booked the appointment; users are deactivated, never deleted.
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.ComplexProperty(a => a.Slot, slot =>
        {
            slot.Property(s => s.Start).HasColumnName("starts_at");
            slot.Property(s => s.End).HasColumnName("ends_at");
        });
    }
}