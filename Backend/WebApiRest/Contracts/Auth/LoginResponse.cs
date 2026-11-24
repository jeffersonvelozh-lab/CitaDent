namespace WebApiRest.Contracts.Auth;

/// <summary>Answer of a successful login. Role is the role name (for example "Reception").</summary>
public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, string Role);
