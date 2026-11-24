using Application.Common;
using Application.Patients;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Patients;

namespace Application.Tests.Patients;

/// <summary>Create and get patients, with in-memory doubles. All data is fictitious.</summary>
public class PatientHandlersTests
{
    // Valid fictitious cédulas (they pass the Ecuadorian check digit).
    private const string Cedula = "0923456784";
    private const string OtherCedula = "0102030400";

    // 2026-12-02 03:00 UTC is 2026-12-01 22:00 in Ecuador: the dates differ, on purpose.
    private static readonly DateTimeOffset UtcNow = new(2026, 12, 2, 3, 0, 0, TimeSpan.Zero);

    private readonly FakePatientRepository _patients = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreatePatientHandler Create => new(_patients, TestClinic.ClockAt(UtcNow), _unitOfWork);
    private GetPatientHandler Get => new(_patients);

    private static CreatePatientCommand Adult(
        string number = Cedula,
        DocumentType type = DocumentType.Cedula) =>
        new(type, number, "Lucía", "Andrade", new DateOnly(1990, 3, 15), Phone: "0991234567");

    private static CreatePatientCommand Minor(DateOnly birthDate, string? guardianName, string? guardianPhone) =>
        new(DocumentType.Cedula, Cedula, "Mateo", "Andrade", birthDate,
            GuardianName: guardianName, GuardianPhone: guardianPhone);

    // Create

    [Fact]
    public async Task Create_AddsAnActivePatientAndSavesOnce()
    {
        var result = await Create.HandleAsync(Adult());

        var saved = Assert.Single(_patients.All);
        Assert.Equal(saved.Id, result.Id);
        Assert.Equal(DocumentType.Cedula, result.DocumentType);
        Assert.Equal(Cedula, result.DocumentNumber);
        Assert.Equal("Lucía", result.FirstName);
        Assert.Equal(new DateOnly(1990, 3, 15), result.BirthDate);
        Assert.Equal("0991234567", result.Phone);
        Assert.True(result.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WithACedulaAlreadyRegistered_ThrowsDuplicateAndDoesNotSave()
    {
        await Create.HandleAsync(Adult());

        var ex = await Assert.ThrowsAsync<DomainException>(() => Create.HandleAsync(Adult()));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
        Assert.DoesNotContain(Cedula, ex.Message); // Personal data stays out of messages and logs.
        Assert.Single(_patients.All);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WithAPassportAlreadyRegisteredInOtherCase_ThrowsDuplicate()
    {
        await Create.HandleAsync(Adult("AB123456", DocumentType.Passport));

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(Adult(" ab123456 ", DocumentType.Passport)));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
    }

    [Fact]
    public async Task Create_SameDigitsAsCedulaAndAsPassport_DoNotClash()
    {
        await Create.HandleAsync(Adult(Cedula, DocumentType.Cedula));

        await Create.HandleAsync(Adult(Cedula, DocumentType.Passport));

        Assert.Equal(2, _patients.All.Count);
    }

    [Fact]
    public async Task Create_AnotherCedula_IsAllowed()
    {
        await Create.HandleAsync(Adult());

        await Create.HandleAsync(Adult(OtherCedula));

        Assert.Equal(2, _patients.All.Count);
    }

    [Fact]
    public async Task Create_WithAnInvalidCedula_ThrowsRn10AndDoesNotSave()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Create.HandleAsync(Adult("0923456785")));

        Assert.Equal("RN-10", ex.Code);
        Assert.Empty(_patients.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Rosa Andrade", null)]
    [InlineData(null, "0987654321")]
    public async Task Create_MinorWithoutCompleteGuardian_ThrowsRn12AndDoesNotSave(string? name, string? phone)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(Minor(new DateOnly(2015, 6, 1), name, phone)));

        Assert.Equal("RN-12", ex.Code);
        Assert.Empty(_patients.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_MinorWithGuardian_IsCreated()
    {
        var result = await Create.HandleAsync(Minor(new DateOnly(2015, 6, 1), "Rosa Andrade", "0987654321"));

        Assert.Equal("Rosa Andrade", result.GuardianName);
        Assert.Equal("0987654321", result.GuardianPhone);
    }

    [Fact]
    public async Task Create_JudgesAgeByTheEcuadorianDate_NotTheUtcDate()
    {
        // Born 2008-12-02: 18 by the UTC date (Dec 2) but still 17 in Ecuador (Dec 1), so a guardian is required.
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(Minor(new DateOnly(2008, 12, 2), null, null)));

        Assert.Equal("RN-12", ex.Code);
    }

    [Fact]
    public async Task Create_TurningEighteenTodayInEcuador_IsAnAdult()
    {
        var result = await Create.HandleAsync(Minor(new DateOnly(2008, 12, 1), null, null));

        Assert.Null(result.GuardianName);
    }

    [Fact]
    public async Task Create_BornAfterTodayInEcuador_ThrowsValidation()
    {
        // Dec 2 is already today in UTC, but tomorrow in Ecuador.
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(Adult() with { BirthDate = new DateOnly(2026, 12, 2) }));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
    }

    [Fact]
    public async Task Create_WithoutFirstName_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(Adult() with { FirstName = " " }));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
    }

    // Get

    [Fact]
    public async Task Get_ReturnsThePatient()
    {
        var created = await Create.HandleAsync(Adult());

        var result = await Get.HandleAsync(new GetPatientQuery(created.Id));

        Assert.Equal(created, result);
    }

    [Fact]
    public async Task Get_UnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Get.HandleAsync(new GetPatientQuery(Guid.NewGuid())));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
    }
}
