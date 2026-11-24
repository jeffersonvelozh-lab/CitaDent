using Application.Common;
using Application.Security;
using Application.Users;
using Domain.Common;

namespace Application.Auth;

/// <summary>
/// Checks a user's email and password and issues an access token. Every failure (unknown email,
/// inactive user, wrong password) gives the same error, so a caller cannot learn which emails exist.
/// </summary>
public sealed class LoginHandler(IUserRepository users, IPasswordHasher passwordHasher, IAccessTokenIssuer tokenIssuer)
    : ICommandHandler<LoginCommand, LoginResult>
{
    public const string InvalidCredentialsCode = "AUTH-INVALID-CREDENTIALS";

    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrEmpty(command.Password))
        {
            throw new DomainException(DomainException.ValidationCode, "Email and password are required.");
        }

        var email = command.Email.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            // Spend the same time as a real check, so response times do not reveal unknown emails.
            passwordHasher.Hash(command.Password);
            throw InvalidCredentials();
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash) || !user.IsActive)
        {
            throw InvalidCredentials();
        }

        var token = tokenIssuer.Issue(user);

        return new LoginResult(token.Value, token.ExpiresAt, user.Role);
    }

    private static DomainException InvalidCredentials() =>
        new(InvalidCredentialsCode, "The email or password is incorrect.");
}
