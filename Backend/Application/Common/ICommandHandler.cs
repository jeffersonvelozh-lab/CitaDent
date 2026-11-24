namespace Application.Common;

/// <summary>
/// Template for a use case. A use case is a command (an immutable record with the input) and a sealed
/// handler with one method. Inside HandleAsync, in this order:
/// 1. Validate the input: missing or malformed data throws DomainException(DomainException.ValidationCode, ...) (HTTP 400).
/// 2. Check the rules that need the database (uniqueness, RN-06...).
/// 3. Call the domain.
/// 4. Save once with <see cref="IUnitOfWork.SaveChangesAsync"/>; repositories never save.
/// AddApplication() registers every implementation, so a new handler needs no extra wiring.
/// See <see cref="Users.SeedInitialAdminHandler"/> for a complete example.
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
