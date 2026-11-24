using Application.Common;

namespace Application.Procedures;

/// <summary>
/// Changes a procedure's data. Existing appointments keep their own time range, so a new duration
/// only affects appointments scheduled from now on (RN-16).
/// </summary>
public sealed class UpdateProcedureHandler(IProcedureRepository procedures, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateProcedureCommand, ProcedureDto>
{
    public async Task<ProcedureDto> HandleAsync(
        UpdateProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var procedure = await procedures.GetByIdAsync(command.Id, cancellationToken)
            ?? throw ProcedureErrors.NotFound(command.Id);

        // Domain: validates the name (VALIDATION) and RN-16, and trims the name. Nothing is saved
        // if a later check fails, because SaveChangesAsync is only called at the end.
        procedure.Update(command.Name, command.Description, command.DurationMinutes, command.BasePrice);

        if (await procedures.NameExistsAsync(procedure.Name, procedure.Id, cancellationToken))
        {
            throw ProcedureErrors.DuplicateName(procedure.Name);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ProcedureDto.From(procedure);
    }
}
