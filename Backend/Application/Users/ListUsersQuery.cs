using Domain.Users;

namespace Application.Users;

/// <summary>
/// The users; inactive ones only when <paramref name="IncludeInactive"/> is true, and only those with
/// <paramref name="Role"/> when it is given. There are few users, so the list is not paged.
/// </summary>
public sealed record ListUsersQuery(bool IncludeInactive = false, UserRole? Role = null);
