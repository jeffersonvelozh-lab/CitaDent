namespace WebApiRest.Contracts.Users;

/// <summary>Body of PUT /api/users/{id}/password: the new password, set by an admin without the old one.</summary>
public sealed record ResetUserPasswordRequest(string? Password);
