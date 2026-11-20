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
}