using Domain.Common;

namespace Domain.Procedures;

/// <summary>
/// A dental procedure in the catalog (cleaning, root canal, ...). Its duration decides how long
/// an appointment lasts (RN-01).
/// </summary>
public sealed class Procedure : Entity<Guid>
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 300;

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public int DurationMinutes { get; private set; }

    public decimal BasePrice { get; private set; }

    public bool IsActive { get; private set; }

    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes);

    // Required by EF Core when it materializes entities.
    private Procedure()
    {
        Name = null!;
    }

    public static Procedure Create(string name, string? description, int durationMinutes, decimal basePrice)
    {
        var procedure = new Procedure
        {
            Id = Guid.CreateVersion7(),
            IsActive = true
        };

        procedure.Apply(name, description, durationMinutes, basePrice);

        return procedure;
    }

    public void Update(string name, string? description, int durationMinutes, decimal basePrice) =>
        Apply(name, description, durationMinutes, basePrice);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private void Apply(string name, string? description, int durationMinutes, decimal basePrice)
    {
        var validName = Guard.NotBlank(name, "Name", NameMaxLength);
        var validDescription = Guard.OptionalMax(description, "Description", DescriptionMaxLength);

        if (durationMinutes <= 0)
        {
            throw new DomainException("RN-16", "The duration of a procedure must be greater than zero.");
        }

        if (basePrice < 0)
        {
            throw new DomainException("RN-16", "The price of a procedure cannot be negative.");
        }

        Name = validName;
        Description = validDescription;
        DurationMinutes = durationMinutes;
        BasePrice = decimal.Round(basePrice, 2, MidpointRounding.AwayFromZero);
    }
}