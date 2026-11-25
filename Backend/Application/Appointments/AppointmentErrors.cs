using Application.Common;
using Domain.Common;

namespace Application.Appointments;

internal static class AppointmentErrors
{
    public const string InThePastCode = "RN-06";
    public const string DentistTakenCode = "RN-02";
    public const string ChairTakenCode = "RN-03";

    public const string NotTheAssignedDentistCode = "RN-09";

    public static DomainException NotTheAssignedDentist() =>
        new(NotTheAssignedDentistCode, "Only the assigned dentist or an administrator can complete the appointment.");

    public static DomainException NotStartedYet(string action) =>
        new(DomainException.ValidationCode, $"The appointment has not started yet, so it cannot be {action}.");

    public static DomainException AlreadyStarted() =>
        new(DomainException.ValidationCode, "The appointment has already started, so it can no longer be cancelled.");

    public static DomainException NotFound(Guid id) =>
        new(ApplicationErrorCodes.NotFound, $"Appointment {id} was not found.");

    public static DomainException UnknownReference(string what, Guid id) =>
        new(DomainException.ValidationCode, $"The {what} {id} does not exist.");

    public static DomainException InThePast() =>
        new(InThePastCode, "An appointment cannot be scheduled in the past.");

    public static DomainException From(ScheduleConflictException conflict) =>
        conflict.Conflict == ScheduleConflict.Chair
            ? new(ChairTakenCode, "The chair is already occupied in that time.")
            : new(DentistTakenCode, "The dentist already has an appointment in that time.");
}
