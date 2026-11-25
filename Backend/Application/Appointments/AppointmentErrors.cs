using Application.Common;
using Domain.Common;

namespace Application.Appointments;

internal static class AppointmentErrors
{
    public const string InThePastCode = "RN-06";
    public const string DentistTakenCode = "RN-02";
    public const string ChairTakenCode = "RN-03";

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
