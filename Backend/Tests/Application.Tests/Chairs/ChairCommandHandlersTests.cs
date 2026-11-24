using Application.Chairs;
using Application.Common;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Scheduling;

namespace Application.Tests.Chairs;

/// <summary>Create, rename and activate/deactivate chairs, with in-memory doubles.</summary>
public class ChairCommandHandlersTests
{
    private readonly FakeChairRepository _chairs = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateChairHandler Create => new(_chairs, _unitOfWork);
    private RenameChairHandler Rename => new(_chairs, _unitOfWork);
    private SetChairActiveHandler SetActive => new(_chairs, _unitOfWork);

    private Chair SeedChair(string name = "Sillón 1") => _chairs.Seed(Chair.Create(name));

    // Create

    [Fact]
    public async Task Create_AddsAnActiveChairWithTrimmedNameAndSavesOnce()
    {
        var result = await Create.HandleAsync(new CreateChairCommand("  Sillón 1 "));

        var saved = Assert.Single(_chairs.All);
        Assert.Equal(saved.Id, result.Id);
        Assert.Equal("Sillón 1", result.Name);
        Assert.True(result.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("Sillón 1")]
    [InlineData("SILLÓN 1")]
    [InlineData("  sillón 1  ")]
    public async Task Create_WithANameAlreadyUsed_ThrowsDuplicateAndDoesNotSave(string name)
    {
        SeedChair();

        var ex = await Assert.ThrowsAsync<DomainException>(() => Create.HandleAsync(new CreateChairCommand(name)));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
        Assert.Single(_chairs.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WithANameUsedByAnInactiveChair_ThrowsDuplicate()
    {
        SeedChair().Deactivate();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(new CreateChairCommand("Sillón 1")));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutName_ThrowsValidationAndDoesNotSave(string name)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Create.HandleAsync(new CreateChairCommand(name)));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Empty(_chairs.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WithATooLongName_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(new CreateChairCommand(new string('A', Chair.NameMaxLength + 1))));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
    }

    // Rename

    [Fact]
    public async Task Rename_ChangesTheNameAndSavesOnce()
    {
        var chair = SeedChair();

        var result = await Rename.HandleAsync(new RenameChairCommand(chair.Id, " Sillón A "));

        Assert.Equal("Sillón A", result.Name);
        Assert.Equal("Sillón A", chair.Name); // The tracked entity changed, so the save writes it.
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Rename_ToItsOwnNameInOtherCase_IsAllowed()
    {
        var chair = SeedChair();

        var result = await Rename.HandleAsync(new RenameChairCommand(chair.Id, "SILLÓN 1"));

        Assert.Equal("SILLÓN 1", result.Name);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Rename_ToTheNameOfAnotherChair_ThrowsDuplicateAndDoesNotSave()
    {
        SeedChair("Sillón 2");
        var chair = SeedChair();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Rename.HandleAsync(new RenameChairCommand(chair.Id, "sillón 2")));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Rename_WithoutName_ThrowsValidationAndKeepsTheOldName()
    {
        var chair = SeedChair();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Rename.HandleAsync(new RenameChairCommand(chair.Id, " ")));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Equal("Sillón 1", chair.Name);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Rename_UnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Rename.HandleAsync(new RenameChairCommand(Guid.NewGuid(), "Sillón 9")));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // Activate and deactivate

    [Fact]
    public async Task Deactivate_KeepsTheChairStoredButInactive()
    {
        var chair = SeedChair();

        var result = await SetActive.HandleAsync(new SetChairActiveCommand(chair.Id, IsActive: false));

        Assert.False(result.IsActive);
        Assert.Same(chair, Assert.Single(_chairs.All));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Activate_TurnsAnInactiveChairBackOn()
    {
        var chair = SeedChair();
        chair.Deactivate();

        var result = await SetActive.HandleAsync(new SetChairActiveCommand(chair.Id, IsActive: true));

        Assert.True(result.IsActive);
        Assert.True(chair.IsActive);
    }

    [Fact]
    public async Task Activate_AnAlreadyActiveChair_StaysActive()
    {
        var chair = SeedChair();

        var result = await SetActive.HandleAsync(new SetChairActiveCommand(chair.Id, IsActive: true));

        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task SetActive_UnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => SetActive.HandleAsync(new SetChairActiveCommand(Guid.NewGuid(), IsActive: false)));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
