using Application.Common;
using Application.Patients;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Patients;

namespace Application.Tests.Patients;

/// <summary>Edit, activate and deactivate patients, with in-memory doubles. All data is fictitious.</summary>
public class PatientUpdateHandlersTests
{
    // Valid fictitious cédulas (they pass the Ecuadorian check digit).
    private const string Cedula = "0923456784";
    private const string OtherCedula = "0102030400";

    // 2026-12-02 03:00 UTC is 2026-12-01 22:00 in Ecuador: the dates differ, on purpose.
    private static readonly DateTimeOffset UtcNow = new(2026, 12, 2, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly SeedDay = new(2026, 11, 1);

    private readonly FakePatientRepository _patients = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private UpdatePatientHandler Update => new(_patients, TestClinic.ClockAt(UtcNow), _unitOfWork);
    private SetPatientActiveHandler SetActive => new(_patients, _unitOfWork);

    private Patient SeedAdult(string cedula = Cedula) =>
        _patients.Seed(Patient.Create(
            DocumentType.Cedula, cedula, "Lucía", "Andrade", new DateOnly(1990, 3, 15), SeedDay, phone: "0991234567"));

    private static UpdatePatientCommand Edit(Guid id, string number = Cedula) =>
        new(id, DocumentType.Cedula, number, "Lucía Fernanda", "Andrade Mora", new DateOnly(1990, 3, 16),
            Phone: "0997654321", Email: "lucia@example.com");

    // Update

    [Fact]
    public async Task Update_ChangesTheDataAndSavesOnce()
    {
        var patient = SeedAdult();

        var result = await Update.HandleAsync(Edit(patient.Id));

        Assert.Equal(patient.Id, result.Id);
        Assert.Equal("Lucía Fernanda", result.FirstName);
        Assert.Equal("Andrade Mora", result.LastName);
        Assert.Equal(new DateOnly(1990, 3, 16), result.BirthDate);
        Assert.Equal("0997654321", result.Phone);
        Assert.Equal("lucia@example.com", result.Email);
        Assert.Equal("Lucía Fernanda", patient.FirstName); // The tracked entity changed, so the save writes it.
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_KeepingItsOwnDocument_IsNotADuplicate()
    {
        var patient = SeedAdult();

        await Update.HandleAsync(Edit(patient.Id, Cedula));

        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_ToTheDocumentOfAnotherPatient_ThrowsDuplicateAndDoesNotSave()
    {
        SeedAdult(OtherCedula);
        var patient = SeedAdult();

        var ex = await Assert.ThrowsAsync<DomainException>(() => Update.HandleAsync(Edit(patient.Id, OtherCedula)));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
        Assert.DoesNotContain(OtherCedula, ex.Message);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_ToANewDocument_IsAllowed()
    {
        var patient = SeedAdult();

        var result = await Update.HandleAsync(Edit(patient.Id) with
        {
            DocumentType = DocumentType.Passport,
            DocumentNumber = "ab123456"
        });

        Assert.Equal(DocumentType.Passport, result.DocumentType);
        Assert.Equal("AB123456", result.DocumentNumber);
    }

    [Fact]
    public async Task Update_UnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Update.HandleAsync(Edit(Guid.NewGuid())));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_WithAnInvalidCedula_ThrowsRn10AndDoesNotSave()
    {
        var patient = SeedAdult();

        var ex = await Assert.ThrowsAsync<DomainException>(() => Update.HandleAsync(Edit(patient.Id, "0923456785")));

        Assert.Equal("RN-10", ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_JudgesAgeByTheEcuadorianDate_NotTheUtcDate()
    {
        // Born 2008-12-02: 18 by the UTC date (Dec 2) but still 17 in Ecuador (Dec 1), so a guardian is required.
        var patient = SeedAdult();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Update.HandleAsync(Edit(patient.Id) with { BirthDate = new DateOnly(2008, 12, 2) }));

        Assert.Equal("RN-12", ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_AnInactivePatient_KeepsItInactive()
    {
        var patient = SeedAdult();
        patient.Deactivate();

        var result = await Update.HandleAsync(Edit(patient.Id));

        Assert.False(result.IsActive);
    }

    // Activate and deactivate (RN-11: deactivate instead of delete)

    [Fact]
    public async Task Deactivate_KeepsThePatientStoredButInactive()
    {
        var patient = SeedAdult();

        var result = await SetActive.HandleAsync(new SetPatientActiveCommand(patient.Id, IsActive: false));

        Assert.False(result.IsActive);
        Assert.Same(patient, Assert.Single(_patients.All));
        Assert.False(patient.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Activate_TurnsAnInactivePatientBackOn()
    {
        var patient = SeedAdult();
        patient.Deactivate();

        var result = await SetActive.HandleAsync(new SetPatientActiveCommand(patient.Id, IsActive: true));

        Assert.True(result.IsActive);
        Assert.True(patient.IsActive);
    }

    [Fact]
    public async Task Deactivate_AnAlreadyInactivePatient_StaysInactive()
    {
        var patient = SeedAdult();
        patient.Deactivate();

        var result = await SetActive.HandleAsync(new SetPatientActiveCommand(patient.Id, IsActive: false));

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task SetActive_UnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => SetActive.HandleAsync(new SetPatientActiveCommand(Guid.NewGuid(), IsActive: false)));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
