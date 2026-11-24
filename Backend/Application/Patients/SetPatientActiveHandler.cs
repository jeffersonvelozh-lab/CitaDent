using Application.Common;

namespace Application.Patients;

/// <summary>
/// Activates or deactivates a patient. Patients are never deleted, only deactivated (RN-11):
/// there is no delete use case, so their appointments and history keep pointing to them.
/// Asking for the state it already has changes nothing.
/// </summary>
public sealed class SetPatientActiveHandler(IPatientRepository patients, IUnitOfWork unitOfWork)
    : ICommandHandler<SetPatientActiveCommand, PatientDto>
{
    public async Task<PatientDto> HandleAsync(SetPatientActiveCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var patient = await patients.GetByIdAsync(command.Id, cancellationToken)
            ?? throw PatientErrors.NotFound(command.Id);

        if (command.IsActive)
        {
            patient.Activate();
        }
        else
        {
            patient.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return PatientDto.From(patient);
    }
}
