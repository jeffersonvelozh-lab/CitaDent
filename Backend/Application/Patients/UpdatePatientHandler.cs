using Application.Common;

namespace Application.Patients;

/// <summary>
/// Changes a patient's data, document included. The domain validates it (RN-10, RN-12, judging age by
/// today's date at the clinic); this handler adds that no other patient has the document.
/// An inactive patient can be edited and stays inactive.
/// </summary>
public sealed class UpdatePatientHandler(IPatientRepository patients, ClinicClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePatientCommand, PatientDto>
{
    public async Task<PatientDto> HandleAsync(UpdatePatientCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var patient = await patients.GetByIdAsync(command.Id, cancellationToken)
            ?? throw PatientErrors.NotFound(command.Id);

        // Nothing is saved if a later check fails, because SaveChangesAsync is only called at the end.
        patient.Update(
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
                patient.DocumentType, patient.DocumentNumber, patient.Id, cancellationToken))
        {
            throw PatientErrors.DuplicateDocument();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return PatientDto.From(patient);
    }
}
