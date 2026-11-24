using Domain.Scheduling;

namespace Application.Dentists;

/// <summary>
/// A dentist as the use cases return it. The schedule is ordered from Monday to Sunday and then by start time.
/// </summary>
public sealed record DentistDto(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string? Specialty,
    string LicenseNumber,
    bool IsActive,
    IReadOnlyList<WorkingHoursDto> WeeklySchedule)
{
    public static DentistDto From(Dentist dentist) => new(
        dentist.Id,
        dentist.UserId,
        dentist.FirstName,
        dentist.LastName,
        dentist.Specialty,
        dentist.LicenseNumber,
        dentist.IsActive,
        dentist.WeeklySchedule
            .OrderBy(h => MondayFirst(h.Day))
            .ThenBy(h => h.Start)
            .Select(WorkingHoursDto.From)
            .ToList());

    // DayOfWeek starts on Sunday (0); the clinic's week starts on Monday.
    private static int MondayFirst(DayOfWeek day) => ((int)day + 6) % 7;
}
