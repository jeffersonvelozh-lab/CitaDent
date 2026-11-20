using Domain.Common;
using Domain.Procedures;
using Xunit;

namespace Domain.Tests.Procedures;

public class ProcedureTests
{
    [Fact]
    public void Create_ReturnsAnActiveProcedure_WithTrimmedName()
    {
        var procedure = Procedure.Create("  Cleaning  ", "Basic cleaning", 30, 25m);

        Assert.NotEqual(Guid.Empty, procedure.Id);
        Assert.Equal("Cleaning", procedure.Name);
        Assert.Equal(TimeSpan.FromMinutes(30), procedure.Duration);
        Assert.Equal(25m, procedure.BasePrice);
        Assert.True(procedure.IsActive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void Create_Throws_WhenDurationIsNotPositive(int minutes)
    {
        var ex = Assert.Throws<DomainException>(() => Procedure.Create("Cleaning", null, minutes, 25m));

        Assert.Equal("RN-16", ex.Code);
    }

    [Fact]
    public void Create_Throws_WhenPriceIsNegative()
    {
        var ex = Assert.Throws<DomainException>(() => Procedure.Create("Cleaning", null, 30, -1m));

        Assert.Equal("RN-16", ex.Code);
    }

    [Fact]
    public void Create_AllowsAFreeProcedure()
    {
        var procedure = Procedure.Create("Check-up", null, 20, 0m);

        Assert.Equal(0m, procedure.BasePrice);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenNameIsBlank(string? name)
    {
        var ex = Assert.Throws<DomainException>(() => Procedure.Create(name!, null, 30, 25m));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void Create_RoundsThePriceToTwoDecimals()
    {
        var procedure = Procedure.Create("Cleaning", null, 30, 10.005m);

        Assert.Equal(10.01m, procedure.BasePrice);
    }

    [Fact]
    public void Update_ChangesTheValues_AndKeepsTheId()
    {
        var procedure = Procedure.Create("Cleaning", null, 30, 25m);
        var id = procedure.Id;

        procedure.Update("Deep cleaning", "With polishing", 45, 40m);

        Assert.Equal(id, procedure.Id);
        Assert.Equal("Deep cleaning", procedure.Name);
        Assert.Equal(45, procedure.DurationMinutes);
        Assert.Equal(40m, procedure.BasePrice);
    }

    [Fact]
    public void Update_Throws_AndKeepsTheOldValues_WhenTheNewOnesAreInvalid()
    {
        var procedure = Procedure.Create("Cleaning", null, 30, 25m);

        Assert.Throws<DomainException>(() => procedure.Update("Deep cleaning", null, 0, 40m));

        Assert.Equal("Cleaning", procedure.Name);
        Assert.Equal(30, procedure.DurationMinutes);
    }

    [Fact]
    public void DeactivateAndActivate_ToggleTheState()
    {
        var procedure = Procedure.Create("Cleaning", null, 30, 25m);

        procedure.Deactivate();
        Assert.False(procedure.IsActive);

        procedure.Activate();
        Assert.True(procedure.IsActive);
    }
}