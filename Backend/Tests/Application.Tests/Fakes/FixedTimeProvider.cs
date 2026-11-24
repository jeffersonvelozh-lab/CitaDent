namespace Application.Tests.Fakes;

/// <summary>A clock stopped at one instant.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
