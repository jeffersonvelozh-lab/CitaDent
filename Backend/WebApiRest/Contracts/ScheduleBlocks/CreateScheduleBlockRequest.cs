using System.ComponentModel.DataAnnotations;

namespace WebApiRest.Contracts.ScheduleBlocks;

/// <summary>
/// Body of POST /api/schedule-blocks:
/// <c>{ "dentistId": null, "startsAt": "2026-12-24T08:00:00-05:00", "endsAt": "2026-12-24T18:00:00-05:00", "reason": "Feriado" }</c>.
/// A null or missing dentistId blocks the whole clinic. Both instants need an offset, and are required because a
/// missing value type would otherwise arrive as its default and look valid.
/// </summary>
public sealed record CreateScheduleBlockRequest(
    Guid? DentistId,
    [Required] DateTimeOffset? StartsAt,
    [Required] DateTimeOffset? EndsAt,
    string? Reason);
