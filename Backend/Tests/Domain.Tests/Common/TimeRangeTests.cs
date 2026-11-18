using Domain.Common;
using Xunit;

namespace Domain.Tests.Common;

public class TimeRangeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);

    private static TimeRange Range(int startMinutes, int endMinutes) =>
        new(T0.AddMinutes(startMinutes), T0.AddMinutes(endMinutes));

    [Theory]
    [InlineData(0)]   // end equals start
    [InlineData(-10)] // end before start
    public void Constructor_Throws_WhenEndIsNotAfterStart(int endOffsetMinutes)
    {
        var ex = Assert.Throws<DomainException>(() => new TimeRange(T0, T0.AddMinutes(endOffsetMinutes)));

        Assert.Equal("RN-01", ex.Code);
    }

    [Fact]
    public void FromStartAndDuration_ComputesTheEnd()
    {
        var range = TimeRange.FromStartAndDuration(T0, TimeSpan.FromMinutes(45));

        Assert.Equal(T0.AddMinutes(45), range.End);
        Assert.Equal(TimeSpan.FromMinutes(45), range.Duration);
    }

    [Fact]
    public void FromStartAndDuration_Throws_WhenDurationIsNotPositive()
    {
        var ex = Assert.Throws<DomainException>(() => TimeRange.FromStartAndDuration(T0, TimeSpan.Zero));

        Assert.Equal("RN-01", ex.Code);
    }

    [Fact]
    public void Constructor_NormalizesToUtc_KeepingTheSameInstant()
    {
        var ecuadorStart = new DateTimeOffset(2026, 12, 1, 4, 0, 0, TimeSpan.FromHours(-5));

        var range = new TimeRange(ecuadorStart, ecuadorStart.AddMinutes(30));

        Assert.Equal(TimeSpan.Zero, range.Start.Offset);
        Assert.Equal(T0, range.Start);
    }

    [Theory]
    [InlineData(0, 30, 15, 45, true)]  // partial overlap
    [InlineData(0, 30, 0, 30, true)]   // identical
    [InlineData(0, 60, 10, 20, true)]  // one contains the other
    [InlineData(0, 30, 30, 60, false)] // back to back (RN-04)
    [InlineData(0, 30, 45, 60, false)] // apart
    public void OverlapsWith_ReturnsExpectedResult_InBothDirections(
        int aStart, int aEnd, int bStart, int bEnd, bool expected)
    {
        var a = Range(aStart, aEnd);
        var b = Range(bStart, bEnd);

        Assert.Equal(expected, a.OverlapsWith(b));
        Assert.Equal(expected, b.OverlapsWith(a));
    }
}