using Application.Common;
using Application.Patients;
using Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class PatientRepository(CitaDentDbContext context) : IPatientRepository
{
    private const string LikeEscape = "\\";

    // Tracked, so a use case can change it and save through the unit of work.
    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Patients.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    // Same columns as the unique index (document_type, document_number).
    public Task<bool> DocumentExistsAsync(
        DocumentType documentType,
        string documentNumber,
        Guid? excludingId,
        CancellationToken cancellationToken = default) =>
        context.Patients.AnyAsync(
            p => p.DocumentType == documentType
                && p.DocumentNumber == documentNumber
                && (excludingId == null || p.Id != excludingId),
            cancellationToken);

    public async Task<PagedResult<Patient>> SearchAsync(
        IReadOnlyList<string> words,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(words);

        var query = context.Patients.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        // One WHERE per word (AND); inside it, ILIKE '%word%' on each column (OR).
        foreach (var word in words)
        {
            var pattern = $"%{EscapeLike(word)}%";

            query = query.Where(p =>
                EF.Functions.ILike(p.FirstName, pattern, LikeEscape)
                || EF.Functions.ILike(p.LastName, pattern, LikeEscape)
                || EF.Functions.ILike(p.DocumentNumber, pattern, LikeEscape));
        }

        var total = await query.CountAsync(cancellationToken);

        // long, so a huge page number cannot overflow; past the end there is nothing to read.
        var offset = (long)(page - 1) * pageSize;

        if (offset >= total)
        {
            return new PagedResult<Patient>([], page, pageSize, total);
        }

        var items = await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ThenBy(p => p.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Patient>(items, page, pageSize, total);
    }

    public void Add(Patient patient) => context.Patients.Add(patient);

    // The user's text is plain text: '%' and '_' must not act as wildcards.
    private static string EscapeLike(string value) =>
        value
            .Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
}
