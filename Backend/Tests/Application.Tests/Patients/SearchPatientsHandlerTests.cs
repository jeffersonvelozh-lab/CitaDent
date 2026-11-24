using Application.Common;
using Application.Patients;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Patients;

namespace Application.Tests.Patients;

/// <summary>Search patients, with an in-memory double. All data is fictitious.</summary>
public class SearchPatientsHandlerTests
{
    private static readonly DateOnly SeedDay = new(2026, 11, 1);

    private readonly FakePatientRepository _patients = new();

    private SearchPatientsHandler Search => new(_patients);

    private Patient Seed(string firstName, string lastName, string passport) =>
        _patients.Seed(Patient.Create(
            DocumentType.Passport, passport, firstName, lastName, new DateOnly(1990, 1, 1), SeedDay));

    [Fact]
    public async Task Search_ReturnsMatchingPatientsAsDtosWithPagingData()
    {
        var ana = Seed("Ana", "Pérez", "AA11111");
        Seed("Luis", "Mora", "BB22222");

        var result = await Search.HandleAsync(new SearchPatientsQuery("ana pér"));

        var item = Assert.Single(result.Items);
        Assert.Equal(ana.Id, item.Id);
        Assert.Equal(1, result.Page);
        Assert.Equal(Paging.DefaultPageSize, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_WithoutTerm_ListsEveryone(string? term)
    {
        Seed("Ana", "Pérez", "AA11111");
        Seed("Luis", "Mora", "BB22222");

        var result = await Search.HandleAsync(new SearchPatientsQuery(term));

        Assert.Equal(2, result.TotalCount);
        Assert.Empty(_patients.LastSearchWords!);
    }

    [Fact]
    public async Task Search_SplitsTheTermIntoWords_IgnoringExtraSpaces()
    {
        await Search.HandleAsync(new SearchPatientsQuery("  ana \t  pérez  "));

        Assert.Equal(["ana", "pérez"], _patients.LastSearchWords!);
    }

    [Fact]
    public async Task Search_WithNoMatches_ReturnsAnEmptyPage()
    {
        Seed("Ana", "Pérez", "AA11111");

        var result = await Search.HandleAsync(new SearchPatientsQuery("zambrano"));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Search_APageBeyondTheEnd_IsEmptyButKeepsTheTotal()
    {
        Seed("Ana", "Pérez", "AA11111");
        Seed("Luis", "Mora", "BB22222");

        var result = await Search.HandleAsync(new SearchPatientsQuery(Page: 5, PageSize: 1));

        Assert.Empty(result.Items);
        Assert.Equal(5, result.Page);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task Search_LeavesOutInactivePatientsUnlessAsked()
    {
        Seed("Ana", "Pérez", "AA11111").Deactivate();

        Assert.Equal(0, (await Search.HandleAsync(new SearchPatientsQuery("ana"))).TotalCount);
        Assert.Equal(1, (await Search.HandleAsync(new SearchPatientsQuery("ana", IncludeInactive: true))).TotalCount);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Search_WithAnInvalidPageOrSize_ThrowsValidation(int page, int pageSize)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Search.HandleAsync(new SearchPatientsQuery(Page: page, PageSize: pageSize)));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Null(_patients.LastSearchWords);
    }

    [Fact]
    public async Task Search_AcceptsTheLargestPageSize()
    {
        var result = await Search.HandleAsync(new SearchPatientsQuery(PageSize: Paging.MaxPageSize));

        Assert.Equal(Paging.MaxPageSize, result.PageSize);
    }

    [Fact]
    public async Task Search_WithATooLongTerm_ThrowsValidation()
    {
        var term = new string('a', SearchPatientsHandler.TermMaxLength + 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Search.HandleAsync(new SearchPatientsQuery(term)));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
    }
}
