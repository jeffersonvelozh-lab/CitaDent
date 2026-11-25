using Application.Common;
using Application.ScheduleBlocks;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Scheduling;

namespace Application.Tests.ScheduleBlocks;

/// <summary>Create, get and remove schedule blocks, with in-memory doubles.</summary>
public class ScheduleBlockHandlersTests
{
    private static readonly TimeSpan Ecuador = TimeSpan.FromHours(-5);

    // December 2, 2026, 09:00 at the clinic.
    private static readonly DateTimeOffset Today = new(2026, 12, 2, 0, 0, 0, Ecuador);
    private static readonly DateTimeOffset UtcNow = Today.AddHours(9).ToUniversalTime();

    private readonly FakeScheduleBlockRepository _blocks = new();
    private readonly FakeDentistRepository _dentists = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateScheduleBlockHandler Create => new(_blocks, _dentists, TestClinic.ClockAt(UtcNow), _unitOfWork);
    private GetScheduleBlockHandler Get => new(_blocks);
    private RemoveScheduleBlockHandler Remove => new(_blocks, _unitOfWork);

    // December 24, 2026 at the clinic.
    private static DateTimeOffset ChristmasEveAt(int hour) => new(2026, 12, 24, hour, 0, 0, Ecuador);

    private Dentist SeedDentist() =>
        _dentists.Seed(Dentist.Create(Guid.CreateVersion7(), "Carla", "Mora", "LIC-001"));

    private static CreateScheduleBlockCommand Command(
        Guid? dentistId = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        string reason = "Feriado") =>
        new(dentistId, startsAt ?? ChristmasEveAt(8), endsAt ?? ChristmasEveAt(18), reason);

    private ScheduleBlock SeedBlock() =>
        _blocks.Seed(ScheduleBlock.Create(null, new TimeRange(ChristmasEveAt(8), ChristmasEveAt(18)), "Feriado"));

    // Create

    [Fact]
    public async Task Create_ForTheWholeClinic_AddsTheBlockInUtcAndSavesOnce()
    {
        var result = await Create.HandleAsync(Command(reason: "  Nochebuena "));

        var saved = Assert.Single(_blocks.All);
        Assert.Equal(saved.Id, result.Id);
        Assert.Null(result.DentistId);
        Assert.Equal(new DateTimeOffset(2026, 12, 24, 13, 0, 0, TimeSpan.Zero), result.StartsAt);
        Assert.Equal(TimeSpan.Zero, result.StartsAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 12, 24, 23, 0, 0, TimeSpan.Zero), result.EndsAt);
        Assert.Equal("Nochebuena", result.Reason);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_ForAnExistingDentist_AddsTheBlock()
    {
        var dentist = SeedDentist();

        var result = await Create.HandleAsync(Command(dentistId: dentist.Id, reason: "Vacaciones"));

        Assert.Equal(dentist.Id, result.DentistId);
        Assert.Equal(dentist.Id, Assert.Single(_blocks.All).DentistId);
    }

    [Fact]
    public async Task Create_ForAnInactiveDentist_IsAllowed()
    {
        var dentist = SeedDentist();
        dentist.Deactivate();

        var result = await Create.HandleAsync(Command(dentistId: dentist.Id));

        Assert.Equal(dentist.Id, result.DentistId);
    }

    [Fact]
    public async Task Create_ForAnUnknownDentist_ThrowsValidationAndDoesNotSave()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(Command(dentistId: Guid.CreateVersion7())));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Create_WithAnEmptyDentistId_ThrowsValidationAndDoesNotSave()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Create.HandleAsync(Command(dentistId: Guid.Empty)));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        AssertNothingSaved();
    }

    [Theory]
    [InlineData(18, 8)]
    [InlineData(8, 8)]
    public async Task Create_WhenTheEndIsNotAfterTheStart_ThrowsValidationAndDoesNotSave(int startHour, int endHour)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(Command(startsAt: ChristmasEveAt(startHour), endsAt: ChristmasEveAt(endHour))));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        AssertNothingSaved();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutAReason_ThrowsValidationAndDoesNotSave(string reason)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Create.HandleAsync(Command(reason: reason)));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Create_WithAReasonTooLong_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(Command(reason: new string('a', ScheduleBlock.ReasonMaxLength + 1))));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
    }

    [Theory]
    [InlineData(8)] // Ended an hour ago.
    [InlineData(9)] // Ends right now: the half-open range covers no instant from now on.
    public async Task Create_WhenTheBlockHasAlreadyEnded_ThrowsValidationAndDoesNotSave(int endHour)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Create.HandleAsync(Command(startsAt: Today.AddHours(7), endsAt: Today.AddHours(endHour))));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Create_WhenTheBlockIsInProgress_IsAllowed()
    {
        await Create.HandleAsync(Command(startsAt: Today.AddHours(7), endsAt: Today.AddHours(12)));

        Assert.Single(_blocks.All);
    }

    [Fact]
    public async Task Create_ABlockThatOverlapsAnotherOne_IsAllowed()
    {
        SeedBlock();

        await Create.HandleAsync(Command(startsAt: ChristmasEveAt(10), endsAt: ChristmasEveAt(20)));

        Assert.Equal(2, _blocks.All.Count);
    }

    // Get

    [Fact]
    public async Task Get_ReturnsTheBlock()
    {
        var block = SeedBlock();

        var result = await Get.HandleAsync(new GetScheduleBlockQuery(block.Id));

        Assert.Equal(ScheduleBlockDto.From(block), result);
    }

    [Fact]
    public async Task Get_WithAnUnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Get.HandleAsync(new GetScheduleBlockQuery(Guid.CreateVersion7())));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
    }

    // Remove

    [Fact]
    public async Task Remove_DeletesTheBlockAndSavesOnce()
    {
        var block = SeedBlock();
        var other = SeedBlock();

        var result = await Remove.HandleAsync(new RemoveScheduleBlockCommand(block.Id));

        Assert.Equal(block.Id, result.Id);
        Assert.Equal(other.Id, Assert.Single(_blocks.All).Id);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Remove_ABlockThatHasEnded_IsAllowed()
    {
        var newYear = new DateTimeOffset(2026, 1, 1, 0, 0, 0, Ecuador);
        var past = _blocks.Seed(ScheduleBlock.Create(null, new TimeRange(newYear, newYear.AddDays(1)), "Año Nuevo"));

        await Remove.HandleAsync(new RemoveScheduleBlockCommand(past.Id));

        Assert.Empty(_blocks.All);
    }

    [Fact]
    public async Task Remove_WithAnUnknownId_ThrowsNotFoundAndDoesNotSave()
    {
        SeedBlock();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Remove.HandleAsync(new RemoveScheduleBlockCommand(Guid.CreateVersion7())));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
        Assert.Single(_blocks.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    private void AssertNothingSaved()
    {
        Assert.Empty(_blocks.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
