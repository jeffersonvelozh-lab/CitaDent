using Application.Tests.Fakes;

namespace Application.Tests.Common;

public class ClinicClockTests
{
    [Fact]
    public void Today_LateAtNightInEcuador_IsStillTheEcuadorianDate()
    {
        // 2026-12-02 03:30 UTC is 2026-12-01 22:30 in Ecuador.
        var clock = TestClinic.ClockAt(new DateTimeOffset(2026, 12, 2, 3, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 12, 1), clock.Today);
    }

    [Fact]
    public void Today_InTheMorning_IsTheSameDateAsUtc()
    {
        var clock = TestClinic.ClockAt(new DateTimeOffset(2026, 12, 2, 14, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 12, 2), clock.Today);
    }

    [Fact]
    public void Now_CarriesTheClinicOffset()
    {
        var clock = TestClinic.ClockAt(new DateTimeOffset(2026, 12, 2, 14, 0, 0, TimeSpan.Zero));

        Assert.Equal(TimeSpan.FromHours(-5), clock.Now.Offset);
        Assert.Equal(9, clock.Now.Hour);
    }
}
