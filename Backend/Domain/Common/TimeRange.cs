namespace Domain.Common;

/// <summary>
/// A half-open time interval [Start, End). Always stored in UTC, which is what PostgreSQL
/// <c>timestamptz</c> expects. Two back-to-back ranges do not overlap (RN-04).
/// </summary>
public sealed record TimeRange
{
    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan Duration => End - Start;

    public TimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new DomainException("RN-01", "The end of a time range must be after its start.");
        }

        Start = start.ToUniversalTime();
        End = end.ToUniversalTime();
    }

    public static TimeRange FromStartAndDuration(DateTimeOffset start, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new DomainException("RN-01", "The duration of a time range must be greater than zero.");
        }

        return new TimeRange(start, start + duration);
    }

    public bool OverlapsWith(TimeRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && other.Start < End;
    }
}