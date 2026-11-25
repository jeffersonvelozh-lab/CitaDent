using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>How CitaDentDbContext connects to PostgreSQL; shared by the API and the integration tests.</summary>
public static class CitaDentDatabaseOptions
{
    /// <summary>
    /// PostgreSQL with snake_case names, retrying transient errors. The retry matters for RN-02 and RN-03: when two
    /// overlapping appointments are inserted at the same time, each exclusion check can wait for the other and
    /// PostgreSQL aborts one with a deadlock (40P01) instead of 23P01. Retried, that insert finds the other
    /// appointment already committed and fails with 23P01, which becomes RN-02 or RN-03. Retrying is safe because
    /// the code opens no explicit transactions: each SaveChanges is one transaction and is retried whole.
    /// </summary>
    public static DbContextOptionsBuilder UseCitaDentDatabase(this DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null))
            .UseSnakeCaseNamingConvention();
    }
}
