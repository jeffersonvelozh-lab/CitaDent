namespace Application.Security;

/// <summary>A signed access token and the moment it stops being valid (UTC).</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
