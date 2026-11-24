using Application.Common;
using Domain.Common;

namespace Application.Chairs;

internal static class ChairErrors
{
    public static DomainException NotFound(Guid id) =>
        new(ApplicationErrorCodes.NotFound, $"Chair {id} was not found.");

    public static DomainException DuplicateName(string name) =>
        new(ApplicationErrorCodes.Duplicate, $"A chair named \"{name}\" already exists.");
}
