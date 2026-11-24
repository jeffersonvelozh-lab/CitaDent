using Application.Common;

namespace Application.Patients;

/// <summary>One patient by id, active or not.</summary>
public sealed class GetPatientHandler(IPatientRepository patients) : IQueryHandler<GetPatientQuery, PatientDto>
{
    public async Task<PatientDto> HandleAsync(GetPatientQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var patient = await patients.GetByIdAsync(query.Id, cancellationToken)
            ?? throw PatientErrors.NotFound(query.Id);

        return PatientDto.From(patient);
    }
}
