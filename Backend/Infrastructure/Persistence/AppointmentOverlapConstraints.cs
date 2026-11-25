using Application.Appointments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// The EXCLUDE constraints of migration AddAppointmentOverlapConstraints, which stop two Scheduled appointments
/// of the same dentist or chair from overlapping (RN-02, RN-03) even when two requests save at the same time.
/// </summary>
internal static class AppointmentOverlapConstraints
{
    public const string Dentist = "ex_appointments_dentist_no_overlap";
    public const string Chair = "ex_appointments_chair_no_overlap";

    /// <summary>
    /// The conflict behind a failed save, or null when the error is something else: another SqlState, or an
    /// exclusion constraint that is not one of these two.
    /// </summary>
    public static ScheduleConflict? Find(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation } postgres
            ? postgres.ConstraintName switch
            {
                Dentist => ScheduleConflict.Dentist,
                Chair => ScheduleConflict.Chair,
                _ => null
            }
            : null;
}
