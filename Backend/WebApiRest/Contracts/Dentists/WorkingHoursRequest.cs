using System.ComponentModel.DataAnnotations;

namespace WebApiRest.Contracts.Dentists;

/// <summary>
/// One block of the body of PUT /api/dentists/{id}/schedule, in the clinic's local time:
/// <c>{ "day": "Monday", "start": "08:00", "end": "12:00" }</c>. Every field is required here because
/// a missing value type would otherwise arrive as its default (Sunday, 00:00) and look valid.
/// </summary>
public sealed record WorkingHoursRequest(
    [Required] DayOfWeek? Day,
    [Required] TimeOnly? Start,
    [Required] TimeOnly? End);
