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

    [Fact]
    public async Task DocumentExists_IsTrueForTheSameTypeAndNumber()
    {
        // The fixture already saved a patient with cédula 1710034065.
        await using var context = fixture.CreateContext();
        var repository = new PatientRepository(context);

        Assert.True(await repository.DocumentExistsAsync(DocumentType.Cedula, fixture.SeedPatient.DocumentNumber));
        Assert.False(await repository.DocumentExistsAsync(DocumentType.Cedula, "0923456784"));
    }

    [Fact]
    public async Task DocumentExists_ComparesTheTypeToo()
    {
        await using var context = fixture.CreateContext();

        Assert.False(await new PatientRepository(context)
            .DocumentExistsAsync(DocumentType.Passport, fixture.SeedPatient.DocumentNumber));
    }

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
        var patient = NewPatient(DocumentType.Passport, $"P{Guid.NewGuid():N}"[..12].ToUpperInvariant());

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
}
