using Application.Common;

namespace Application.Procedures;

/// <summary>
/// Activates or deactivates a procedure. Procedures are never deleted, because appointments keep
/// their id. Asking for the state it already has changes nothing.
/// </summary>
public sealed class SetProcedureActiveHandler(IProcedureRepository procedures, IUnitOfWork unitOfWork)
    : ICommandHandler<SetProcedureActiveCommand, ProcedureDto>
{
    public async Task<ProcedureDto> HandleAsync(
        SetProcedureActiveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var procedure = await procedures.GetByIdAsync(command.Id, cancellationToken)
            ?? throw ProcedureErrors.NotFound(command.Id);

        if (command.IsActive)
        {
            procedure.Activate();
        }
        else
        {
            procedure.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ProcedureDto.From(procedure);
    }
}
