using Application.Common;
using Domain.Patients;

namespace Application.Patients;

/// <summary>
/// Registers a patient. The domain validates the document (RN-10) and asks a minor for a guardian (RN-12),
/// judging age by today's date at the clinic; this handler adds that the document is unique.
/// </summary>
public sealed class CreatePatientHandler(IPatientRepository patients, ClinicClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePatientCommand, PatientDto>
{
    public async Task<PatientDto> HandleAsync(CreatePatientCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Domain first: it validates and normalizes the document, which the uniqueness check needs.
        var patient = Patient.Create(
            command.DocumentType,
            command.DocumentNumber,
            command.FirstName,
            command.LastName,
            command.BirthDate,
            clock.Today,
            command.Phone,
            command.Email,
            command.Address,
            command.GuardianName,
            command.GuardianPhone);

        if (await patients.DocumentExistsAsync(
                patient.DocumentType, patient.DocumentNumber, excludingId: null, cancellationToken))
        {
            throw PatientErrors.DuplicateDocument();
        }

        patients.Add(patient);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return PatientDto.From(patient);
    }
}
