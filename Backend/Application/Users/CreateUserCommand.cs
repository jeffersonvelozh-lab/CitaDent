using Domain.Users;

namespace Application.Users;

/// <summary>A new reception or admin account. Dentists are registered with their dentist record.</summary>
public sealed record CreateUserCommand(string Email, string FullName, string Password, UserRole Role);
