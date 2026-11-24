using Application.Common;

namespace Application.Dentists;

/// <summary>One dentist by id with the weekly schedule, active or not.</summary>
public sealed class GetDentistHandler(IDentistRepository dentists) : IQueryHandler<GetDentistQuery, DentistDto>
{
    public async Task<DentistDto> HandleAsync(GetDentistQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dentist = await dentists.GetByIdAsync(query.Id, cancellationToken)
            ?? throw DentistErrors.NotFound(query.Id);

        return DentistDto.From(dentist);
    }
}
