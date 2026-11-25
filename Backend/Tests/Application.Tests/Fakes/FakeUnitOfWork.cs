using Application.Common;

namespace Application.Tests.Fakes;

/// <summary>Counts how many times a use case saves. Set <see cref="FailWith"/> to make the save fail.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    /// <summary>When set, SaveChangesAsync throws it instead of saving, as the database would.</summary>
    public Exception? FailWith { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        SaveCount++;
        return Task.FromResult(1);
    }
}
