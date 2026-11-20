using Domain.Common;

namespace Domain.Patients;

public sealed class Patient : Entity<Guid>
{
    public const int AdultAge = 18;

    public DocumentType DocumentType { get; private set; }

    public string DocumentNumber { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public DateOnly BirthDate { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? Address { get; private set; }

    public string? GuardianName { get; private set; }

    public string? GuardianPhone { get; private set; }

    public bool IsActive { get; private set; }

    // Required by EF Core when it materializes entities.
    private Patient()
    {
        DocumentNumber = null!;
        FirstName = null!;
        LastName = null!;
    }

    /// <param name="today">
    /// The current date, supplied by the caller so the domain stays deterministic and easy to test.
    /// </param>
    public static Patient Create(
        DocumentType documentType,
        string documentNumber,
        string firstName,
        string lastName,
        DateOnly birthDate,
        DateOnly today,
        string? phone = null,
        string? email = null,
        string? address = null,
        string? guardianName = null,
        string? guardianPhone = null)
    {
        if (birthDate > today)
        {
            throw new DomainException(Guard.ValidationCode, "The birth date cannot be in the future.");
        }

        var patient = new Patient
        {
            Id = Guid.CreateVersion7(),
            DocumentType = documentType,
            DocumentNumber = NormalizeDocument(documentType, documentNumber),
            FirstName = Guard.NotBlank(firstName, "First name", 80),
            LastName = Guard.NotBlank(lastName, "Last name", 80),
            BirthDate = birthDate,
            Phone = Guard.OptionalMax(phone, "Phone", 20),
            Email = Guard.OptionalMax(email, "Email", 150),
            Address = Guard.OptionalMax(address, "Address", 200),
            GuardianName = Guard.OptionalMax(guardianName, "Guardian name", 160),
            GuardianPhone = Guard.OptionalMax(guardianPhone, "Guardian phone", 20),
            IsActive = true
        };

        if (patient.IsMinor(today) && (patient.GuardianName is null || patient.GuardianPhone is null))
        {
            throw new DomainException("RN-12", "A minor patient requires a guardian with name and phone.");
        }

        return patient;
    }

    public int AgeOn(DateOnly date)
    {
        var age = date.Year - BirthDate.Year;

        if (date < BirthDate.AddYears(age))
        {
            age--;
        }

        return age;
    }

    public bool IsMinor(DateOnly today) => AgeOn(today) < AdultAge;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static string NormalizeDocument(DocumentType documentType, string? documentNumber)
    {
        var value = Guard.NotBlank(documentNumber, "Document number", 20);

        switch (documentType)
        {
            case DocumentType.Cedula:
                if (!EcuadorianId.IsValidCedula(value))
                {
                    throw new DomainException("RN-10", "The cédula is not valid.");
                }

                return value;

            case DocumentType.Passport:
                if (value.Length < 5 || !value.All(char.IsAsciiLetterOrDigit))
                {
                    throw new DomainException("RN-10", "The passport number is not valid.");
                }

                return value.ToUpperInvariant();

            default:
                throw new DomainException(Guard.ValidationCode, "Unknown document type.");
        }
    }
}