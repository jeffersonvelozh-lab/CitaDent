using Domain.Common;

namespace Domain.Users;

/// <summary>
/// A person who can sign in to CitaDent. The domain only stores the password hash; hashing happens
/// behind a port in Application. Email uniqueness is enforced by the database and Application,
/// because the domain cannot see other users.
/// </summary>
public sealed class User : Entity<Guid>
{
    public const int EmailMaxLength = 150;
    public const int FullNameMaxLength = 160;

    public string Email { get; private set; }

    public string FullName { get; private set; }

    public string PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    // Required by EF Core when it materializes entities.
    private User()
    {
        Email = null!;
        FullName = null!;
        PasswordHash = null!;
    }

    public static User Create(string email, string fullName, string passwordHash, UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException(Guard.ValidationCode, "Unknown user role.");
        }

        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = NormalizeEmail(email),
            FullName = Guard.NotBlank(fullName, "Full name", FullNameMaxLength),
            PasswordHash = RequireHash(passwordHash),
            Role = role,
            IsActive = true
        };
    }

    /// <summary>Changes the display name; if it is invalid, the old one stays.</summary>
    public void ChangeFullName(string fullName) =>
        FullName = Guard.NotBlank(fullName, "Full name", FullNameMaxLength);

    /// <summary>
    /// Changes the email, normalized and checked like in <see cref="Create"/>; if it is invalid, the old one
    /// stays. Uniqueness is checked by Application.
    /// </summary>
    public void ChangeEmail(string email) => Email = NormalizeEmail(email);

    /// <summary>Replaces the password hash (a password reset); if it is blank, the old one stays.</summary>
    public void ChangePasswordHash(string passwordHash) => PasswordHash = RequireHash(passwordHash);

    /// <summary>
    /// Moves the user between Admin and Reception. The Dentist role is tied to a dentist record, so a user
    /// cannot move into it or out of it. Keeping at least one active admin is checked by Application.
    /// </summary>
    public void ChangeRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException(Guard.ValidationCode, "Unknown user role.");
        }

        if (Role == UserRole.Dentist || role == UserRole.Dentist)
        {
            throw new DomainException(
                Guard.ValidationCode, "The dentist role cannot be given or taken away; it belongs to a dentist record.");
        }

        Role = role;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static string RequireHash(string? passwordHash) =>
        string.IsNullOrWhiteSpace(passwordHash)
            ? throw new DomainException(Guard.ValidationCode, "Password hash is required.")
            : passwordHash;

    /// <summary>
    /// Trims and lowercases the email, then applies a simple format check: exactly one '@',
    /// a non-empty local part, a domain with at least one inner dot, and no whitespace.
    /// </summary>
    private static string NormalizeEmail(string? email)
    {
        var value = Guard.NotBlank(email, "Email", EmailMaxLength).ToLowerInvariant();

        var at = value.IndexOf('@');
        var isValid = at > 0
            && at == value.LastIndexOf('@')
            && !value.Any(char.IsWhiteSpace);

        if (isValid)
        {
            var domain = value[(at + 1)..];
            var dot = domain.IndexOf('.');
            isValid = dot > 0 && !domain.EndsWith('.');
        }

        if (!isValid)
        {
            throw new DomainException(Guard.ValidationCode, "Email format is not valid.");
        }

        return value;
    }
}
