namespace Application.Common;

/// <summary>
/// The clinic's local time. The domain has no clock: use cases ask this class for "today" or "now"
/// in the clinic's time zone (America/Guayaquil) and pass it to the domain. Near midnight the
/// date in UTC is already the next day, so never use the UTC date as "today".
/// </summary>
public sealed class ClinicClock(TimeProvider timeProvider, TimeZoneInfo timeZone)
{
    public TimeZoneInfo TimeZone { get; } = timeZone ?? throw new ArgumentNullException(nameof(timeZone));

    /// <summary>The current instant, with the clinic's offset (for example -05:00).</summary>
    public DateTimeOffset Now => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), TimeZone);

    /// <summary>Today's date at the clinic.</summary>
    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);
}
