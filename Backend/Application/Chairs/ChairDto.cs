using Domain.Scheduling;

namespace Application.Chairs;

/// <summary>A chair as the use cases return it.</summary>
public sealed record ChairDto(Guid Id, string Name, bool IsActive)
{
    public static ChairDto From(Chair chair) => new(chair.Id, chair.Name, chair.IsActive);
}
