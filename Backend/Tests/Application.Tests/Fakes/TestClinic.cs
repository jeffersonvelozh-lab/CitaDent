using Application.Common;

namespace Application.Tests.Fakes;

internal static class TestClinic
{
    // Ecuador has no daylight saving time, so a fixed UTC-5 zone behaves like America/Guayaquil on any machine.
    public static readonly TimeZoneInfo Ecuador =
        TimeZoneInfo.CreateCustomTimeZone("ecuador-test", TimeSpan.FromHours(-5), "Ecuador", "Ecuador");

    /// <summary>A clinic clock stopped at <paramref name="utcNow"/>.</summary>
    public static ClinicClock ClockAt(DateTimeOffset utcNow) => new(new FixedTimeProvider(utcNow), Ecuador);
}
