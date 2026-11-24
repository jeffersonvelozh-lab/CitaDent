namespace Application.Common;

/// <summary>
/// Template for a read-only use case: a query (an immutable record with the filters) and a sealed handler.
/// A query handler validates its input like a command handler but never changes state nor saves.
/// AddApplication() registers every implementation.
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
