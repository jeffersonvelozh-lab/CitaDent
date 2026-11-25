using Domain.Common;

namespace Domain.Scheduling;

public sealed class Dentist : Entity<Guid>
{
    public const int NameMaxLength = 80;
    public const int SpecialtyMaxLength = 80;
    public const int LicenseNumberMaxLength = 30;

    private readonly List<WorkingHours> _weeklySchedule = [];

    public Guid UserId { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string? Specialty { get; private set; }

    public string LicenseNumber { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyList<WorkingHours> WeeklySchedule => _weeklySchedule;

    // Required by EF Core when it materializes entities.
    private Dentist()
    {
        FirstName = null!;
        LastName = null!;
        LicenseNumber = null!;
    }

    public static Dentist Create(
        Guid userId,
        string firstName,
        string lastName,
        string licenseNumber,
        string? specialty = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException(Guard.ValidationCode, "A dentist must be linked to a user.");
        }

        var dentist = new Dentist
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            IsActive = true
        };

        dentist.Apply(firstName, lastName, licenseNumber, specialty);

        return dentist;
    }

    /// <summary>
    /// Replaces the dentist's personal data; a blank specialty clears it. The user, the state and the
    /// schedule do not change. License uniqueness is checked by Application, which can see other dentists.
    /// </summary>
    public void Update(string firstName, string lastName, string licenseNumber, string? specialty) =>
        Apply(firstName, lastName, licenseNumber, specialty);

    // Validates everything first and assigns at the end, so a failed Update leaves the dentist as it was.
    private void Apply(string firstName, string lastName, string licenseNumber, string? specialty)
    {
        var validFirstName = Guard.NotBlank(firstName, "First name", NameMaxLength);
        var validLastName = Guard.NotBlank(lastName, "Last name", NameMaxLength);
        var validLicenseNumber = Guard.NotBlank(licenseNumber, "License number", LicenseNumberMaxLength);
        var validSpecialty = Guard.OptionalMax(specialty, "Specialty", SpecialtyMaxLength);

        FirstName = validFirstName;
        LastName = validLastName;
        LicenseNumber = validLicenseNumber;
        Specialty = validSpecialty;
    }

    /// <summary>
    /// Replaces the whole weekly schedule. Blocks on the same day cannot overlap; two blocks that
    /// touch (12:00 end, 12:00 start) are allowed. If the new schedule is invalid, the old one stays.
    /// </summary>
    public void SetWeeklySchedule(IEnumerable<WorkingHours> hours)
    {
        ArgumentNullException.ThrowIfNull(hours);

        var candidate = hours.ToList();

        foreach (var day in candidate.GroupBy(h => h.Day))
        {
            var ordered = day.OrderBy(h => h.Start).ToList();

            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Start < ordered[i - 1].End)
                {
                    throw new DomainException(Guard.ValidationCode, $"Working hours overlap on {day.Key}.");
                }
            }
        }

        _weeklySchedule.Clear();
        _weeklySchedule.AddRange(candidate);
    }

    /// <summary>
    /// True when the whole slot fits inside one working block of the dentist, judged in the clinic's
    /// local time zone. A slot that crosses midnight, or spans a break between blocks, does not fit (RN-05).
    /// </summary>
    public bool WorksDuring(TimeRange slot, TimeZoneInfo clinicTimeZone)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(clinicTimeZone);

        var start = TimeZoneInfo.ConvertTime(slot.Start, clinicTimeZone);
        var end = TimeZoneInfo.ConvertTime(slot.End, clinicTimeZone);

        if (DateOnly.FromDateTime(start.DateTime) != DateOnly.FromDateTime(end.DateTime))
        {
            return false;
        }

        var startTime = TimeOnly.FromDateTime(start.DateTime);
        var endTime = TimeOnly.FromDateTime(end.DateTime);

        return _weeklySchedule.Any(h => h.Day == start.DayOfWeek && h.Start <= startTime && endTime <= h.End);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}