using Application.Common;
using Domain.Procedures;

namespace Application.Procedures;

/// <summary>Adds a procedure to the catalog. Names are unique, ignoring case.</summary>
public sealed class CreateProcedureHandler(IProcedureRepository procedures, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateProcedureCommand, ProcedureDto>
{
    public async Task<ProcedureDto> HandleAsync(
        CreateProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Domain first: it validates the name (VALIDATION) and RN-16, and trims the name.
        var procedure = Procedure.Create(
            command.Name, command.Description, command.DurationMinutes, command.BasePrice);

        if (await procedures.NameExistsAsync(procedure.Name, excludingId: null, cancellationToken))
        {
            throw ProcedureErrors.DuplicateName(procedure.Name);
        }

        procedures.Add(procedure);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ProcedureDto.From(procedure);
    }
}
