using Domain.Common;

namespace Domain.Scheduling;

/// <summary>
/// A period when nobody can be scheduled: a dentist's vacation, or a holiday for the whole clinic
/// (when <see cref="DentistId"/> is null). Used by RN-05.
/// </summary>
public sealed class ScheduleBlock : Entity<Guid>
{
    public const int ReasonMaxLength = 200;

    public Guid? DentistId { get; private set; }

    public TimeRange Range { get; private set; }

    public string Reason { get; private set; }

    // Required by EF Core when it materializes entities.
    private ScheduleBlock()
    {
        Range = null!;
        Reason = null!;
    }

    public static ScheduleBlock Create(Guid? dentistId, TimeRange range, string reason)
    {
        ArgumentNullException.ThrowIfNull(range);

        if (dentistId == Guid.Empty)
        {
            throw new DomainException(Guard.ValidationCode, "The dentist id is not valid.");
        }

        return new ScheduleBlock
        {
            Id = Guid.CreateVersion7(),
            DentistId = dentistId,
            Range = range,
            Reason = Guard.NotBlank(reason, "Reason", ReasonMaxLength)
        };
    }

    /// <summary>
    /// True when this block applies to the given dentist and overlaps the slot.
    /// </summary>
    public bool Affects(Guid dentistId, TimeRange slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        return (DentistId is null || DentistId == dentistId) && Range.OverlapsWith(slot);
    }
}