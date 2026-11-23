using Domain.Users;

namespace Application.Auth;

public sealed record LoginResult(string AccessToken, DateTimeOffset ExpiresAt, UserRole Role);
