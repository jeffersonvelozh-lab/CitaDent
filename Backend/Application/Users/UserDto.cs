using Domain.Users;

namespace Application.Users;

/// <summary>A user account as the use cases return it. The password hash is never part of it.</summary>
public sealed record UserDto(Guid Id, string Email, string FullName, UserRole Role, bool IsActive)
{
    public static UserDto From(User user) => new(user.Id, user.Email, user.FullName, user.Role, user.IsActive);
}
