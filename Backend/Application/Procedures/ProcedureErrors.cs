using Application.Common;
using Domain.Common;

namespace Application.Procedures;

internal static class ProcedureErrors
{
    public static DomainException NotFound(Guid id) =>
        new(ApplicationErrorCodes.NotFound, $"Procedure {id} was not found.");

    public static DomainException DuplicateName(string name) =>
        new(ApplicationErrorCodes.Duplicate, $"A procedure named \"{name}\" already exists.");
}
