using Application.Common;
using Application.Users;

namespace Application.Dentists;

/// <summary>
/// Edits a dentist's personal data and keeps the full name of their user account in step, saved at once.
/// The license number stays unique ignoring case (DUPLICATE); the dentist's own license in other letter
/// case is fine. The email, the password, the state and the schedule do not change here.
/// </summary>
public sealed class UpdateDentistHandler(IDentistRepository dentists, IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateDentistCommand, DentistDto>
{
    public async Task<DentistDto> HandleAsync(UpdateDentistCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var dentist = await dentists.GetByIdAsync(command.Id, cancellationToken)
            ?? throw DentistErrors.NotFound(command.Id);

        // The foreign key guarantees the user; a missing one is broken data, not a caller's mistake.
        var user = await users.GetByIdAsync(dentist.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"Dentist {dentist.Id} has no user {dentist.UserId}.");

        // Domain: validates and trims (VALIDATION). Nothing is saved if a later check fails, because
        // SaveChangesAsync is only called at the end.
        dentist.Update(command.FirstName, command.LastName, command.LicenseNumber, command.Specialty);

        if (await dentists.LicenseNumberExistsAsync(dentist.LicenseNumber, dentist.Id, cancellationToken))
        {
            throw DentistErrors.DuplicateLicenseNumber();
        }

        user.ChangeFullName(DentistUserName.From(dentist.FirstName, dentist.LastName));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DentistDto.From(dentist);
    }
}
