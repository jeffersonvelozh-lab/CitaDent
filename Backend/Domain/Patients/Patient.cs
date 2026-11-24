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
        var patient = new Patient
        {
            Id = Guid.CreateVersion7(),
            IsActive = true
        };

        patient.Apply(
            documentType, documentNumber, firstName, lastName, birthDate, today,
            phone, email, address, guardianName, guardianPhone);

        return patient;
    }

    /// <summary>
    /// Replaces the patient's data with the same rules as <see cref="Create"/> (RN-10, RN-12).
    /// The id and the active state do not change. If a rule fails, nothing changes.
    /// Uniqueness of the document is checked by the use case, not here.
    /// </summary>
    /// <param name="today">The current date at the clinic, supplied by the caller.</param>
    public void Update(
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
        string? guardianPhone = null) =>
        Apply(
            documentType, documentNumber, firstName, lastName, birthDate, today,
            phone, email, address, guardianName, guardianPhone);

    public int AgeOn(DateOnly date) => Age(BirthDate, date);

    public bool IsMinor(DateOnly today) => AgeOn(today) < AdultAge;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    // Validates everything first and assigns at the end, so a failed Update leaves the patient as it was.
    private void Apply(
        DocumentType documentType,
        string documentNumber,
        string firstName,
        string lastName,
        DateOnly birthDate,
        DateOnly today,
        string? phone,
        string? email,
        string? address,
        string? guardianName,
        string? guardianPhone)
    {
        if (birthDate > today)
        {
            throw new DomainException(Guard.ValidationCode, "The birth date cannot be in the future.");
        }

        var validDocument = NormalizeDocument(documentType, documentNumber);
        var validFirstName = Guard.NotBlank(firstName, "First name", 80);
        var validLastName = Guard.NotBlank(lastName, "Last name", 80);
        var validPhone = Guard.OptionalMax(phone, "Phone", 20);
        var validEmail = Guard.OptionalMax(email, "Email", 150);
        var validAddress = Guard.OptionalMax(address, "Address", 200);
        var validGuardianName = Guard.OptionalMax(guardianName, "Guardian name", 160);
        var validGuardianPhone = Guard.OptionalMax(guardianPhone, "Guardian phone", 20);

        if (Age(birthDate, today) < AdultAge && (validGuardianName is null || validGuardianPhone is null))
        {
            throw new DomainException("RN-12", "A minor patient requires a guardian with name and phone.");
        }

        DocumentType = documentType;
        DocumentNumber = validDocument;
        FirstName = validFirstName;
        LastName = validLastName;
        BirthDate = birthDate;
        Phone = validPhone;
        Email = validEmail;
        Address = validAddress;
        GuardianName = validGuardianName;
        GuardianPhone = validGuardianPhone;
    }

    private static int Age(DateOnly birthDate, DateOnly date)
    {
        var age = date.Year - birthDate.Year;

        if (date < birthDate.AddYears(age))
        {
            age--;
        }

        return age;
    }

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