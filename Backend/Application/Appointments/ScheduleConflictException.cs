namespace Application.Appointments;

/// <summary>Which resource the database found already taken.</summary>
public enum ScheduleConflict
{
    Dentist = 1,
    Chair = 2
}

/// <summary>
/// Thrown by <see cref="Common.IUnitOfWork.SaveChangesAsync"/> when the database rejects an appointment because
/// another one, saved at the same time, already takes the dentist or the chair. The domain checked the
/// scheduling context, but the concurrent request was not in it yet. The use case turns it into RN-02 or RN-03.
/// </summary>
public sealed class ScheduleConflictException(ScheduleConflict conflict, Exception? innerException = null)
    : Exception($"Another appointment already takes the {conflict.ToString().ToLowerInvariant()} in that time.", innerException)
{
    public ScheduleConflict Conflict { get; } = conflict;
}
