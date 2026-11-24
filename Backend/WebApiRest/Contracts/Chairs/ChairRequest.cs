namespace WebApiRest.Contracts.Chairs;

/// <summary>Body of POST /api/chairs and PUT /api/chairs/{id}.</summary>
public sealed record ChairRequest(string? Name);
