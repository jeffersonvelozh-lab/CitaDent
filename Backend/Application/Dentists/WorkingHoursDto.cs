using Domain.Scheduling;

namespace Application.Dentists;

/// <summary>One block of weekly working time, in the clinic's local time.</summary>
public sealed record WorkingHoursDto(DayOfWeek Day, TimeOnly Start, TimeOnly End)
{
    public static WorkingHoursDto From(WorkingHours hours) => new(hours.Day, hours.Start, hours.End);

    /// <summary>The domain checks that the day exists and that the block ends after it starts (VALIDATION).</summary>
    public WorkingHours ToDomain() => new(Day, Start, End);
}
