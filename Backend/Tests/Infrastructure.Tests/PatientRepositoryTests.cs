using Domain.Patients;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>All data is fictitious.</summary>
public class PatientRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Today = new(2026, 11, 24);

    private static Patient NewPatient(DocumentType type, string number) =>
        Patient.Create(type, number, "Lucía", "Andrade", new DateOnly(1990, 3, 15), Today);

    private static string NewPassport() => $"P{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    // The database is shared by the whole class, so every search test puts a unique mark in the
    // last names it creates and searches for it. Letters only, so ordering does not depend on digits.
    private static string NewMark() =>
        "zz" + new string(Guid.NewGuid().ToString("N").Select(c => (char)('a' + (c % 26))).Take(10).ToArray());

    private Patient SavePatient(string firstName, string lastName, string? passport = null, bool active = true)
    {
        var patient = Patient.Create(
            DocumentType.Passport, passport ?? NewPassport(), firstName, lastName, new DateOnly(1990, 3, 15), Today);

        if (!active)
        {
            patient.Deactivate();
        }

        fixture.Save(patient);
        return patient;
    }

    private async Task<Application.Common.PagedResult<Patient>> SearchAsync(
        string term,
        bool includeInactive = false,
        int page = 1,
        int pageSize = 20)
    {
        await using var context = fixture.CreateContext();
        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return await new PatientRepository(context).SearchAsync(words, includeInactive, page, pageSize);
    }

    // DocumentExists

    [Fact]
    public async Task DocumentExists_IsTrueForTheSameTypeAndNumber()
    {
        // The fixture already saved a patient with cédula 1710034065.
        await using var context = fixture.CreateContext();
        var repository = new PatientRepository(context);

        Assert.True(await repository.DocumentExistsAsync(
            DocumentType.Cedula, fixture.SeedPatient.DocumentNumber, excludingId: null));
        Assert.False(await repository.DocumentExistsAsync(DocumentType.Cedula, "0923456784", excludingId: null));
    }

    [Fact]
    public async Task DocumentExists_ComparesTheTypeToo()
    {
        await using var context = fixture.CreateContext();

        Assert.False(await new PatientRepository(context)
            .DocumentExistsAsync(DocumentType.Passport, fixture.SeedPatient.DocumentNumber, excludingId: null));
    }

    [Fact]
    public async Task DocumentExists_IgnoresTheExcludedPatient()
    {
        await using var context = fixture.CreateContext();
        var repository = new PatientRepository(context);
        var seed = fixture.SeedPatient;

        Assert.False(await repository.DocumentExistsAsync(DocumentType.Cedula, seed.DocumentNumber, seed.Id));
        Assert.True(await repository.DocumentExistsAsync(DocumentType.Cedula, seed.DocumentNumber, Guid.NewGuid()));
    }

    // GetById and Add

    [Fact]
    public async Task GetById_ReturnsTheStoredPatientOrNull()
    {
        await using var context = fixture.CreateContext();
        var repository = new PatientRepository(context);

        var found = await repository.GetByIdAsync(fixture.SeedPatient.Id);

        Assert.NotNull(found);
        Assert.Equal(fixture.SeedPatient.DocumentNumber, found.DocumentNumber);
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Add_WritesNothingUntilTheUnitOfWorkSaves()
    {
        var patient = NewPatient(DocumentType.Passport, NewPassport());

        await using var context = fixture.CreateContext();
        new PatientRepository(context).Add(patient);

        await using (var before = fixture.CreateContext())
        {
            Assert.Null(await new PatientRepository(before).GetByIdAsync(patient.Id));
        }

        await context.SaveChangesAsync();

        await using var after = fixture.CreateContext();
        var stored = await new PatientRepository(after).GetByIdAsync(patient.Id);
        Assert.NotNull(stored);
        Assert.Equal(DocumentType.Passport, stored.DocumentType);
    }

    [Fact]
    public async Task ChangesToATrackedPatient_AreSavedByTheUnitOfWork()
    {
        var mark = NewMark();
        var patient = SavePatient("Lucía", mark);

        await using (var context = fixture.CreateContext())
        {
            var tracked = await new PatientRepository(context).GetByIdAsync(patient.Id);
            tracked!.Update(DocumentType.Passport, "XY98765", "Lucía", mark + "mora", new DateOnly(1990, 3, 15), Today);
            tracked.Deactivate();
            await context.SaveChangesAsync();
        }

        await using var after = fixture.CreateContext();
        var stored = await new PatientRepository(after).GetByIdAsync(patient.Id);
        Assert.Equal("XY98765", stored!.DocumentNumber);
        Assert.Equal(mark + "mora", stored.LastName);
        Assert.False(stored.IsActive);
    }

    // Search

    [Fact]
    public async Task Search_ByPartOfTheLastName_IgnoresCase()
    {
        var mark = NewMark();
        // ASCII only: how ILIKE folds accented letters depends on the database locale.
        var patient = SavePatient("Ana", "Andrade" + mark);

        var result = await SearchAsync(("drade" + mark[..6]).ToUpperInvariant());

        Assert.Equal(patient.Id, Assert.Single(result.Items).Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByPartOfTheFirstName()
    {
        var mark = NewMark();
        var patient = SavePatient("Fernanda" + mark, "Mora");

        var result = await SearchAsync("nanda" + mark);

        Assert.Equal(patient.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Search_ByPartOfTheDocument_IgnoresCase()
    {
        var passport = NewPassport();
        var patient = SavePatient("Ana", NewMark(), passport);

        var result = await SearchAsync(passport[3..10].ToLowerInvariant());

        Assert.Equal(patient.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Search_EveryWordMustMatch_InAnyColumn()
    {
        var mark = NewMark();
        var ana = SavePatient("Ana", mark);
        SavePatient("Luis", mark);

        var result = await SearchAsync($"an {mark}");

        Assert.Equal(ana.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Search_WithNoMatches_ReturnsNoItemsAndZero()
    {
        SavePatient("Ana", NewMark());

        var result = await SearchAsync(NewMark());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Search_APageBeyondTheEnd_ReturnsNoItemsButTheTotal()
    {
        var mark = NewMark();
        SavePatient("Ana", mark);
        SavePatient("Luis", mark);

        var result = await SearchAsync(mark, page: 3, pageSize: 2);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.Page);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Search_AHugePageNumber_DoesNotOverflow()
    {
        var mark = NewMark();
        SavePatient("Ana", mark);

        var result = await SearchAsync(mark, page: int.MaxValue, pageSize: 100);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_PagesByLastNameThenFirstName_WithoutOverlap()
    {
        var mark = NewMark();
        var third = SavePatient("Ana", mark + "c");
        var first = SavePatient("Bruno", mark + "a");
        var second = SavePatient("Carla", mark + "a");

        var page1 = await SearchAsync(mark, page: 1, pageSize: 2);
        var page2 = await SearchAsync(mark, page: 2, pageSize: 2);

        Assert.Equal([first.Id, second.Id], page1.Items.Select(p => p.Id));
        Assert.Equal([third.Id], page2.Items.Select(p => p.Id));
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(3, page2.TotalCount);
    }

    [Fact]
    public async Task Search_LeavesOutInactivePatientsUnlessAsked()
    {
        var mark = NewMark();
        var active = SavePatient("Ana", mark);
        var inactive = SavePatient("Luis", mark, active: false);

        var onlyActive = await SearchAsync(mark);
        var all = await SearchAsync(mark, includeInactive: true);

        Assert.Equal(active.Id, Assert.Single(onlyActive.Items).Id);
        Assert.Equal(2, all.TotalCount);
        Assert.Contains(all.Items, p => p.Id == inactive.Id);
    }

    [Theory]
    [InlineData("_")]
    [InlineData("%")]
    public async Task Search_TreatsWildcardsAsPlainText(string wildcard)
    {
        var mark = NewMark();
        var literal = SavePatient("Ana", $"a{wildcard}b{mark}");
        SavePatient("Luis", $"axb{mark}");

        var result = await SearchAsync($"a{wildcard}b{mark}");

        Assert.Equal(literal.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Search_TreatsTheEscapeCharacterAsPlainText()
    {
        var mark = NewMark();
        var literal = SavePatient("Ana", $"a\\b{mark}");
        SavePatient("Luis", $"ab{mark}");

        var result = await SearchAsync($"a\\b{mark}");

        Assert.Equal(literal.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Search_ReturnsUntrackedPatients()
    {
        var mark = NewMark();
        SavePatient("Ana", mark);

        await using var context = fixture.CreateContext();
        await new PatientRepository(context).SearchAsync([mark], includeInactive: false, page: 1, pageSize: 20);

        Assert.Empty(context.ChangeTracker.Entries());
    }
}
