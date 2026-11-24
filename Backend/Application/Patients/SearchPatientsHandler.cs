using Application.Common;
using Domain.Common;

namespace Application.Patients;

/// <summary>
/// Searches patients by name or document, a page at a time. "ana pér" finds "Ana Pérez": the term is
/// split into words and each one must appear somewhere. Accents count ("perez" does not find "Pérez").
/// </summary>
public sealed class SearchPatientsHandler(IPatientRepository patients)
    : IQueryHandler<SearchPatientsQuery, PagedResult<PatientDto>>
{
    public const int TermMaxLength = 100;

    public async Task<PagedResult<PatientDto>> HandleAsync(
        SearchPatientsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        Paging.Validate(query.Page, query.PageSize);

        var term = query.Term?.Trim() ?? string.Empty;

        if (term.Length > TermMaxLength)
        {
            throw new DomainException(
                DomainException.ValidationCode, $"The search term must have at most {TermMaxLength} characters.");
        }

        var words = term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var result = await patients.SearchAsync(
            words, query.IncludeInactive, query.Page, query.PageSize, cancellationToken);

        return result.Map(PatientDto.From);
    }
}
