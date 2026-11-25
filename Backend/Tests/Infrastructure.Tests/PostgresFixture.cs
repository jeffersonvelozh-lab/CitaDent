using Domain.Patients;
using Domain.Procedures;
using Domain.Users;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Tests;

/// <summary>
/// Creates a throwaway PostgreSQL database for one test class, applies the real migrations to it
/// and drops it at the end. It needs a PostgreSQL server: the one from docker-compose (port 5433) by default,
/// or the one in the CITADENT_TEST_ADMIN_CONNECTION environment variable.
/// </summary>
public sealed class PostgresFixture : IDisposable
{
    private const string DefaultAdminConnection =
        "Host=localhost;Port=5433;Database=postgres;Username=citadent;Password=citadent_dev";

    private readonly string _adminConnectionString;
    private readonly string _databaseName = $"citadent_test_{Guid.NewGuid():N}";

    public string ConnectionString { get; }

    /// <summary>A patient already saved in the database, for tests that need one.</summary>
    public Patient SeedPatient { get; }

    /// <summary>A 30-minute procedure already saved in the database.</summary>
    public Procedure SeedProcedure { get; }

    /// <summary>A reception user already saved in the database, used as the creator of appointments.</summary>
    public User SeedUser { get; }

    public PostgresFixture()
    {
        _adminConnectionString =
            Environment.GetEnvironmentVariable("CITADENT_TEST_ADMIN_CONNECTION") ?? DefaultAdminConnection;

        ExecuteAsAdmin($"CREATE DATABASE \"{_databaseName}\"");

        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = _databaseName
        }.ConnectionString;

        using (var context = CreateContext())
        {
            // Runs every migration, including the SQL of the overlap constraints.
            context.Database.Migrate();
        }

        SeedPatient = Patient.Create(
            DocumentType.Cedula, "1710034065", "Ana", "Pérez", new DateOnly(1990, 5, 20), new DateOnly(2026, 11, 18));
        SeedProcedure = Procedure.Create("Cleaning", null, 30, 25m);

        SeedUser = User.Create("reception@citadent.test", "Seed Reception", "test-hash", UserRole.Reception);

        Save(SeedPatient, SeedProcedure, SeedUser);
    }

    public CitaDentDbContext CreateContext()
    {
        // The same configuration as the API, retries included.
        var options = new DbContextOptionsBuilder<CitaDentDbContext>();
        options.UseCitaDentDatabase(ConnectionString);

        return new CitaDentDbContext(options.Options, TimeProvider.System);
    }

    /// <summary>Saves the entities with a fresh context, like a separate request would.</summary>
    public void Save(params object[] entities)
    {
        using var context = CreateContext();
        context.AddRange(entities);
        context.SaveChanges();
    }

    public void Dispose()
    {
        NpgsqlConnection.ClearAllPools();
        ExecuteAsAdmin($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    private void ExecuteAsAdmin(string sql)
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();

        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}