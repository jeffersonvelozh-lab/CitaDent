using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class CitaDentDbContext(DbContextOptions<CitaDentDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Needed later by the exclusion constraints that prevent overlapping appointments (RN-02, RN-03).
        modelBuilder.HasPostgresExtension("btree_gist");

        // Entity configurations (one class per entity) are picked up automatically from this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CitaDentDbContext).Assembly);
    }
}