using Application.Common;
using Application.Procedures;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Procedures;

namespace Application.Tests.Procedures;

/// <summary>Create, update and activate/deactivate procedures, with in-memory doubles.</summary>
public class ProcedureCommandHandlersTests
{
    private readonly FakeProcedureRepository _procedures = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateProcedureHandler Create => new(_procedures, _unitOfWork);
    private UpdateProcedureHandler Update => new(_procedures, _unitOfWork);
    private SetProcedureActiveHandler SetActive => new(_procedures, _unitOfWork);

    private Procedure SeedCleaning() => _procedures.Seed(Procedure.Create("Limpieza", null, 30, 25m));

    // Create

    [Fact]
    public async Task Create_AddsAnActiveProcedureAndSavesOnce()
    {
        var result = await Create.HandleAsync(new CreateProcedureCommand("  Endodoncia ", "Conducto", 90, 180.555m));

        var saved = Assert.Single(_procedures.All);
        Assert.Equal(saved.Id, result.Id);
        Assert.Equal("Endodoncia", result.Name);
        Assert.Equal("Conducto", result.Description);
        Assert.Equal(90, result.DurationMinutes);
        Assert.Equal(180.56m, result.BasePrice);
        Assert.True(result.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("Limpieza")]
    [InlineData("LIMPIEZA")]
    [InlineData("  limpieza  ")]
    public async Task Create_WithANameAlreadyUsed_ThrowsDuplicateAndDoesNotSave(string name)
    {
        SeedCleaning();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(new CreateProcedureCommand(name, null, 30, 25m)));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
        Assert.Single(_procedures.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(-15, 25)]
    [InlineData(30, -1)]
    public async Task Create_BreakingRn16_ThrowsAndDoesNotSave(int minutes, decimal price)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(new CreateProcedureCommand("Extracción", null, minutes, price)));

        Assert.Equal("RN-16", ex.Code);
        Assert.Empty(_procedures.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WithoutName_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(new CreateProcedureCommand("  ", null, 30, 25m)));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // Update

    [Fact]
    public async Task Update_ChangesTheDataAndSavesOnce()
    {
        var cleaning = SeedCleaning();

        var result = await Update.HandleAsync(
            new UpdateProcedureCommand(cleaning.Id, "Limpieza profunda", "Con ultrasonido", 45, 40m));

        Assert.Equal("Limpieza profunda", result.Name);
        Assert.Equal("Con ultrasonido", result.Description);
        Assert.Equal(45, result.DurationMinutes);
        Assert.Equal(40m, result.BasePrice);
        Assert.Equal(45, cleaning.DurationMinutes);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_KeepingItsOwnName_IsAllowed()
    {
        var cleaning = SeedCleaning();

        var result = await Update.HandleAsync(new UpdateProcedureCommand(cleaning.Id, "LIMPIEZA", null, 30, 30m));

        Assert.Equal("LIMPIEZA", result.Name);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_WithTheNameOfAnotherProcedure_ThrowsDuplicateAndDoesNotSave()
    {
        SeedCleaning();
        var extraction = _procedures.Seed(Procedure.Create("Extracción", null, 40, 50m));

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Update.HandleAsync(new UpdateProcedureCommand(extraction.Id, "limpieza", null, 40, 50m)));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_UnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Update.HandleAsync(new UpdateProcedureCommand(Guid.NewGuid(), "Limpieza", null, 30, 25m)));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_BreakingRn16_ThrowsAndDoesNotSave()
    {
        var cleaning = SeedCleaning();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Update.HandleAsync(new UpdateProcedureCommand(cleaning.Id, "Limpieza", null, 0, 25m)));

        Assert.Equal("RN-16", ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // Activate / deactivate

    [Fact]
    public async Task SetActive_False_DeactivatesAndSaves()
    {
        var cleaning = SeedCleaning();

        var result = await SetActive.HandleAsync(new SetProcedureActiveCommand(cleaning.Id, IsActive: false));

        Assert.False(result.IsActive);
        Assert.False(cleaning.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SetActive_True_ReactivatesAnInactiveProcedure()
    {
        var cleaning = SeedCleaning();
        cleaning.Deactivate();

        var result = await SetActive.HandleAsync(new SetProcedureActiveCommand(cleaning.Id, IsActive: true));

        Assert.True(result.IsActive);
        Assert.True(cleaning.IsActive);
    }

    [Fact]
    public async Task SetActive_ToTheStateItAlreadyHas_KeepsIt()
    {
        var cleaning = SeedCleaning();

        var result = await SetActive.HandleAsync(new SetProcedureActiveCommand(cleaning.Id, IsActive: true));

        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task SetActive_UnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => SetActive.HandleAsync(new SetProcedureActiveCommand(Guid.NewGuid(), IsActive: false)));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
