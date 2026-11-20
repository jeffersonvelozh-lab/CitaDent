using Domain.Common;

namespace Domain.Scheduling;

/// <summary>
/// One block of weekly working time, in the clinic's local time (for example Monday 08:00 to 12:00).
/// A dentist can have several blocks on the same day, which leaves room for a lunch break.
/// </summary>
public sealed record WorkingHours
{
    public DayOfWeek Day { get; private set; }

    public TimeOnly Start { get; private set; }

    public TimeOnly End { get; private set; }

    // Required by EF Core, which fills the properties when it reads a row. Domain code uses the public constructor.
    private WorkingHours()
    {
    }

    public WorkingHours(DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        if (!Enum.IsDefined(day))
        {
            throw new DomainException(Guard.ValidationCode, "The day of the week is not valid.");
        }

        if (end <= start)
        {
            throw new DomainException(Guard.ValidationCode, "Working hours must end after they start.");
        }

        Day = day;
        Start = start;
        End = end;
    }
}