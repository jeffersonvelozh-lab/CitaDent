using Domain.Common;

namespace Domain.Scheduling;

/// <summary>
/// A dental chair: a physical resource that an appointment occupies (RN-03).
/// </summary>
public sealed class Chair : Entity<Guid>
{
    public const int NameMaxLength = 50;

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    // Required by EF Core when it materializes entities.
    private Chair()
    {
        Name = null!;
    }

    public static Chair Create(string name) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = Guard.NotBlank(name, "Name", NameMaxLength),
        IsActive = true
    };

    public void Rename(string name) => Name = Guard.NotBlank(name, "Name", NameMaxLength);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}