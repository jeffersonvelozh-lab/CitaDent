namespace Application.Users;

/// <summary>The new password an admin sets for a user. It is only used to compute its hash.</summary>
public sealed record ResetUserPasswordCommand(Guid Id, string Password);
