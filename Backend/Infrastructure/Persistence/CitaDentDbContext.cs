using Domain.Appointments;
using Domain.Common;
using Domain.Patients;
using Domain.Procedures;
using Domain.Scheduling;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class CitaDentDbContext(DbContextOptions<CitaDentDbContext> options, TimeProvider timeProvider)
    : DbContext(options)
{
    private const string CreatedAtProperty = "CreatedAt";
    private const string UpdatedAtProperty = "UpdatedAt";

    public DbSet<Procedure> Procedures => Set<Procedure>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Dentist> Dentists => Set<Dentist>();

    public DbSet<Chair> Chairs => Set<Chair>();

    public DbSet<ScheduleBlock> ScheduleBlocks => Set<ScheduleBlock>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Needed by the exclusion constraints that prevent overlapping appointments (RN-02, RN-03).
        modelBuilder.HasPostgresExtension("btree_gist");

        // One configuration class per entity, picked up automatically from this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CitaDentDbContext).Assembly);

        // Every table carries CreatedAt and UpdatedAt. They are not part of the domain model,
        // so they are shadow properties that SaveChanges fills in. Only domain entities get them:
        // value objects such as TimeRange are complex types, and touching them here would turn them into tables.
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && typeof(Entity<Guid>).IsAssignableFrom(t.ClrType))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            modelBuilder.Entity(entityType.ClrType).Property<DateTimeOffset>(CreatedAtProperty);
            modelBuilder.Entity(entityType.ClrType).Property<DateTimeOffset>(UpdatedAtProperty);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        var now = timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Metadata.FindProperty(UpdatedAtProperty) is null)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(CreatedAtProperty).CurrentValue = now;
                entry.Property(UpdatedAtProperty).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(UpdatedAtProperty).CurrentValue = now;
            }
        }
    }
}