using System.ComponentModel.DataAnnotations;
using Domain.Users;

namespace WebApiRest.Contracts.Users;

/// <summary>
/// Body of POST /api/users: a reception or admin account. The role travels as text ("Reception"); it is
/// required here because a missing enum would otherwise arrive as its default. The password is only used
/// to compute its hash; it is never returned.
/// </summary>
public sealed record CreateUserRequest(
    string? Email,
    string? FullName,
    string? Password,
    [Required] UserRole? Role);
