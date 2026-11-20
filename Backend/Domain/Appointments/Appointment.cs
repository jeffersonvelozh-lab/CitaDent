using Domain.Common;
using Domain.Patients;
using Domain.Procedures;
using Domain.Scheduling;

namespace Domain.Appointments;

public sealed class Appointment : Entity<Guid>
{
    public const int CancelReasonMaxLength = 200;

    public Guid PatientId { get; private set; }

    public Guid DentistId { get; private set; }

    public Guid ChairId { get; private set; }

    public Guid ProcedureId { get; private set; }

    public TimeRange Slot { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public string? CancelReason { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    // Required by EF Core when it materializes entities.
    private Appointment()
    {
        Slot = null!;
    }

    /// <summary>
    /// Creates a scheduled appointment if every rule allows it:
    /// RN-01 the end is the start plus the procedure duration;
    /// RN-05 the slot fits the dentist's working hours and avoids blocked periods;
    /// RN-02 and RN-03 the dentist and the chair are free (only scheduled appointments occupy time);
    /// RN-04 two back-to-back appointments are not a conflict.
    /// Not checked here: RN-06 (not in the past) and RN-09 (who can complete), which belong to the application layer.
    /// </summary>
    public static Appointment Schedule(
        Patient patient,
        Dentist dentist,
        Chair chair,
        Procedure procedure,
        DateTimeOffset start,
        Guid createdByUserId,
        SchedulingContext context)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(dentist);
        ArgumentNullException.ThrowIfNull(chair);
        ArgumentNullException.ThrowIfNull(procedure);
        ArgumentNullException.ThrowIfNull(context);

        EnsureActive(patient.IsActive, "patient");
        EnsureActive(dentist.IsActive, "dentist");
        EnsureActive(chair.IsActive, "chair");
        EnsureActive(procedure.IsActive, "procedure");

        if (createdByUserId == Guid.Empty)
        {
            throw new DomainException(Guard.ValidationCode, "The user who schedules the appointment is required.");
        }

        var slot = TimeRange.FromStartAndDuration(start, procedure.Duration);

        if (!dentist.WorksDuring(slot, context.ClinicTimeZone))
        {
            throw new DomainException("RN-05", "The slot is outside the dentist's working hours.");
        }

        if (context.Blocks.Any(block => block.Affects(dentist.Id, slot)))
        {
            throw new DomainException("RN-05", "The slot falls in a blocked period.");
        }

        var overlapping = context.ExistingAppointments
            .Where(a => a.Status == AppointmentStatus.Scheduled && a.Slot.OverlapsWith(slot))
            .ToList();

        if (overlapping.Any(a => a.DentistId == dentist.Id))
        {
            throw new DomainException("RN-02", "The dentist already has an appointment in that time.");
        }

        if (overlapping.Any(a => a.ChairId == chair.Id))
        {
            throw new DomainException("RN-03", "The chair is already occupied in that time.");
        }

        return new Appointment
        {
            Id = Guid.CreateVersion7(),
            PatientId = patient.Id,
            DentistId = dentist.Id,
            ChairId = chair.Id,
            ProcedureId = procedure.Id,
            Slot = slot,
            Status = AppointmentStatus.Scheduled,
            CreatedByUserId = createdByUserId
        };
    }

    public void Complete()
    {
        EnsureScheduled();
        Status = AppointmentStatus.Completed;
    }

    public void MarkNoShow()
    {
        EnsureScheduled();
        Status = AppointmentStatus.NoShow;
    }

    /// <summary>
    /// Cancels the appointment and frees the dentist and the chair for that time (RN-08).
    /// </summary>
    public void Cancel(string reason)
    {
        EnsureScheduled();

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("RN-08", "A reason is required to cancel an appointment.");
        }

        CancelReason = Guard.NotBlank(reason, "Reason", CancelReasonMaxLength);
        Status = AppointmentStatus.Cancelled;
    }

    private void EnsureScheduled()
    {
        if (Status != AppointmentStatus.Scheduled)
        {
            throw new DomainException(
                "RN-07",
                $"Only a scheduled appointment can change state, and this one is {Status}.");
        }
    }

    private static void EnsureActive(bool isActive, string what)
    {
        if (!isActive)
        {
            throw new DomainException(Guard.ValidationCode, $"The {what} is not active.");
        }
    }
}