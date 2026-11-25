namespace Application.ScheduleBlocks;

/// <summary>
/// The blocks that touch the clinic days <paramref name="From"/> to <paramref name="To"/>, both included.
/// With <paramref name="DentistId"/>, only that dentist's blocks; without it, every dentist's.
/// <paramref name="IncludeClinicWide"/> adds the blocks of the whole clinic.
/// </summary>
public sealed record ListScheduleBlocksQuery(DateOnly From, DateOnly To, Guid? DentistId, bool IncludeClinicWide);
