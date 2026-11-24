using Domain.Common;
using Domain.Patients;
using Xunit;

namespace Domain.Tests.Patients;

public class PatientTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);
    private const string ValidCedula = "1710034065";

    private static Patient AdultWithCedula() =>
        Patient.Create(DocumentType.Cedula, ValidCedula, "Ana", "Pérez", new DateOnly(1990, 5, 20), Today);

    private static Patient WithBirthDate(DateOnly birthDate) =>
        Patient.Create(
            DocumentType.Cedula, ValidCedula, "Ana", "Pérez", birthDate, Today,
            guardianName: "Luis Pérez", guardianPhone: "0991234567");

    [Fact]
    public void Create_ReturnsAnActivePatient_ForAnAdultWithAValidCedula()
    {
        var patient = AdultWithCedula();

        Assert.NotEqual(Guid.Empty, patient.Id);
        Assert.Equal(ValidCedula, patient.DocumentNumber);
        Assert.Equal("Ana", patient.FirstName);
        Assert.True(patient.IsActive);
        Assert.False(patient.IsMinor(Today));
    }

    [Fact]
    public void Create_Throws_RN10_WhenTheCedulaIsInvalid()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Patient.Create(DocumentType.Cedula, "1710034066", "Ana", "Pérez", new DateOnly(1990, 5, 20), Today));

        Assert.Equal("RN-10", ex.Code);
    }

    [Fact]
    public void Create_AcceptsAPassport_AndUppercasesIt()
    {
        var patient = Patient.Create(
            DocumentType.Passport, "ab12345", "John", "Smith", new DateOnly(1985, 1, 1), Today);

        Assert.Equal("AB12345", patient.DocumentNumber);
    }

    [Fact]
    public void Create_Throws_RN10_WhenThePassportHasSymbols()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Patient.Create(DocumentType.Passport, "AB-1234", "John", "Smith", new DateOnly(1985, 1, 1), Today));

        Assert.Equal("RN-10", ex.Code);
    }

    [Fact]
    public void Create_Throws_RN12_WhenAMinorHasNoGuardian()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Patient.Create(DocumentType.Cedula, ValidCedula, "Sofía", "Pérez", new DateOnly(2015, 3, 10), Today));

        Assert.Equal("RN-12", ex.Code);
    }

    [Fact]
    public void Create_Throws_RN12_WhenAMinorHasOnlyTheGuardianName()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Patient.Create(
                DocumentType.Cedula, ValidCedula, "Sofía", "Pérez", new DateOnly(2015, 3, 10), Today,
                guardianName: "Luis Pérez"));

        Assert.Equal("RN-12", ex.Code);
    }

    [Fact]
    public void Create_Succeeds_WhenAMinorHasAGuardian()
    {
        var patient = WithBirthDate(new DateOnly(2015, 3, 10));

        Assert.True(patient.IsMinor(Today));
        Assert.Equal("Luis Pérez", patient.GuardianName);
    }

    [Fact]
    public void Create_Throws_WhenTheBirthDateIsInTheFuture()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Patient.Create(DocumentType.Cedula, ValidCedula, "Ana", "Pérez", Today.AddDays(1), Today));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenTheFirstNameIsBlank(string? firstName)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Patient.Create(DocumentType.Cedula, ValidCedula, firstName!, "Pérez", new DateOnly(1990, 5, 20), Today));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Theory]
    [InlineData(2008, 11, 18, false)] // turns 18 today
    [InlineData(2008, 11, 19, true)]  // turns 18 tomorrow
    [InlineData(1990, 5, 20, false)]
    public void IsMinor_UsesTheExactBirthday(int year, int month, int day, bool expected)
    {
        var patient = WithBirthDate(new DateOnly(year, month, day));

        Assert.Equal(expected, patient.IsMinor(Today));
    }

    [Fact]
    public void DeactivateAndActivate_ToggleTheState()
    {
        var patient = AdultWithCedula();

        patient.Deactivate();
        Assert.False(patient.IsActive);

        patient.Activate();
        Assert.True(patient.IsActive);
    }

    // Update

    [Fact]
    public void Update_ReplacesTheData_AndKeepsTheIdAndState()
    {
        var patient = AdultWithCedula();
        var id = patient.Id;
        patient.Deactivate();

        patient.Update(
            DocumentType.Passport, " cd98765 ", " Ana María ", "Pérez Vera", new DateOnly(1991, 6, 1), Today,
            phone: "0991112233", email: "ana@example.com", address: "Av. Siempre Viva 123");

        Assert.Equal(id, patient.Id);
        Assert.False(patient.IsActive);
        Assert.Equal(DocumentType.Passport, patient.DocumentType);
        Assert.Equal("CD98765", patient.DocumentNumber);
        Assert.Equal("Ana María", patient.FirstName);
        Assert.Equal("Pérez Vera", patient.LastName);
        Assert.Equal(new DateOnly(1991, 6, 1), patient.BirthDate);
        Assert.Equal("0991112233", patient.Phone);
        Assert.Equal("ana@example.com", patient.Email);
        Assert.Equal("Av. Siempre Viva 123", patient.Address);
    }

    [Fact]
    public void Update_ClearsOptionalFieldsThatAreLeftOut()
    {
        var patient = Patient.Create(
            DocumentType.Cedula, ValidCedula, "Ana", "Pérez", new DateOnly(1990, 5, 20), Today,
            phone: "0991234567", email: "ana@example.com");

        patient.Update(DocumentType.Cedula, ValidCedula, "Ana", "Pérez", new DateOnly(1990, 5, 20), Today);

        Assert.Null(patient.Phone);
        Assert.Null(patient.Email);
    }

    [Fact]
    public void Update_Throws_RN10_WhenTheCedulaIsInvalid()
    {
        var patient = AdultWithCedula();

        var ex = Assert.Throws<DomainException>(() =>
            patient.Update(DocumentType.Cedula, "1710034066", "Ana", "Pérez", new DateOnly(1990, 5, 20), Today));

        Assert.Equal("RN-10", ex.Code);
    }

    [Fact]
    public void Update_Throws_RN12_WhenTheNewBirthDateMakesAMinorWithoutGuardian()
    {
        var patient = AdultWithCedula();

        var ex = Assert.Throws<DomainException>(() =>
            patient.Update(DocumentType.Cedula, ValidCedula, "Ana", "Pérez", new DateOnly(2015, 3, 10), Today));

        Assert.Equal("RN-12", ex.Code);
    }

    [Fact]
    public void Update_Throws_RN12_WhenTheGuardianIsRemovedFromAMinor()
    {
        var patient = WithBirthDate(new DateOnly(2015, 3, 10));

        var ex = Assert.Throws<DomainException>(() =>
            patient.Update(
                DocumentType.Cedula, ValidCedula, "Ana", "Pérez", new DateOnly(2015, 3, 10), Today,
                guardianName: "Luis Pérez"));

        Assert.Equal("RN-12", ex.Code);
    }

    [Fact]
    public void Update_Throws_WhenTheBirthDateIsInTheFuture()
    {
        var patient = AdultWithCedula();

        var ex = Assert.Throws<DomainException>(() =>
            patient.Update(DocumentType.Cedula, ValidCedula, "Ana", "Pérez", Today.AddDays(1), Today));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void Update_ThatFails_LeavesThePatientUnchanged()
    {
        var patient = AdultWithCedula();

        // The names are valid, but the minor has no guardian: nothing may be half applied.
        Assert.Throws<DomainException>(() =>
            patient.Update(DocumentType.Passport, "XY12345", "Sofía", "Mora", new DateOnly(2015, 3, 10), Today));

        Assert.Equal(DocumentType.Cedula, patient.DocumentType);
        Assert.Equal(ValidCedula, patient.DocumentNumber);
        Assert.Equal("Ana", patient.FirstName);
        Assert.Equal("Pérez", patient.LastName);
        Assert.Equal(new DateOnly(1990, 5, 20), patient.BirthDate);
    }
}