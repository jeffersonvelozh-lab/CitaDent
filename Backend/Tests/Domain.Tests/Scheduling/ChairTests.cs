using Domain.Common;
using Domain.Scheduling;
using Xunit;

namespace Domain.Tests.Scheduling;

public class ChairTests
{
    [Fact]
    public void Create_ReturnsAnActiveChair_WithTrimmedName()
    {
        var chair = Chair.Create("  Chair 1  ");

        Assert.NotEqual(Guid.Empty, chair.Id);
        Assert.Equal("Chair 1", chair.Name);
        Assert.True(chair.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenTheNameIsBlank(string? name)
    {
        var ex = Assert.Throws<DomainException>(() => Chair.Create(name!));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void Create_Throws_WhenTheNameIsTooLong()
    {
        var ex = Assert.Throws<DomainException>(() => Chair.Create(new string('A', Chair.NameMaxLength + 1)));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void Rename_ChangesTheName()
    {
        var chair = Chair.Create("Chair 1");

        chair.Rename("Chair 2");

        Assert.Equal("Chair 2", chair.Name);
    }

    [Fact]
    public void DeactivateAndActivate_ToggleTheState()
    {
        var chair = Chair.Create("Chair 1");

        chair.Deactivate();
        Assert.False(chair.IsActive);

        chair.Activate();
        Assert.True(chair.IsActive);
    }
}