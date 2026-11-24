namespace Application.Users;

/// <summary>The first administrator's data, read from configuration (Seed:Admin:*).</summary>
public sealed record SeedInitialAdminCommand(string Email, string FullName, string Password);
