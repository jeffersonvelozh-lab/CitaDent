namespace Application.ScheduleBlocks;

/// <summary>A block for one dentist, or for the whole clinic when <paramref name="DentistId"/> is null.</summary>
public sealed record CreateScheduleBlockCommand(Guid? DentistId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason);
