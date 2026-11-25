using Domain.Users;

namespace Application.Users;

public sealed record ChangeUserRoleCommand(Guid Id, UserRole Role);
