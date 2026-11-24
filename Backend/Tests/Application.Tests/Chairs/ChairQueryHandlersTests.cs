using Application.Chairs;
using Application.Common;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Scheduling;

namespace Application.Tests.Chairs;

/// <summary>Get and list chairs, with an in-memory double.</summary>
public class ChairQueryHandlersTests
{
    private readonly FakeChairRepository _chairs = new();

    private GetChairHandler Get => new(_chairs);
    private ListChairsHandler List => new(_chairs);

    [Fact]
    public async Task Get_ReturnsTheChairEvenIfInactive()
    {
        var chair = _chairs.Seed(Chair.Create("Sillón 1"));
        chair.Deactivate();

        var result = await Get.HandleAsync(new GetChairQuery(chair.Id));

        Assert.Equal(new ChairDto(chair.Id, "Sillón 1", false), result);
    }

    [Fact]
    public async Task Get_UnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Get.HandleAsync(new GetChairQuery(Guid.NewGuid())));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
    }

    [Fact]
    public async Task List_ByDefault_ReturnsOnlyActiveChairsOrderedByName()
    {
        var second = _chairs.Seed(Chair.Create("Sillón B"));
        var first = _chairs.Seed(Chair.Create("Sillón A"));
        _chairs.Seed(Chair.Create("Sillón C")).Deactivate();

        var result = await List.HandleAsync(new ListChairsQuery());

        Assert.Equal([first.Id, second.Id], result.Select(c => c.Id));
    }

    [Fact]
    public async Task List_WithInactive_IncludesThem()
    {
        _chairs.Seed(Chair.Create("Sillón A"));
        var inactive = _chairs.Seed(Chair.Create("Sillón C"));
        inactive.Deactivate();

        var result = await List.HandleAsync(new ListChairsQuery(IncludeInactive: true));

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Id == inactive.Id && !c.IsActive);
    }

    [Fact]
    public async Task List_WithNoChairs_ReturnsAnEmptyList()
    {
        Assert.Empty(await List.HandleAsync(new ListChairsQuery()));
    }
}
