namespace Application.Security;

/// <summary>
/// Port for turning a plain password into a one-way hash and checking a password against it.
/// The domain only ever stores the resulting hash.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Returns a self-describing hash with its own random salt.</summary>
    string Hash(string password);

    /// <summary>
    /// True when <paramref name="password"/> matches <paramref name="passwordHash"/>.
    /// A malformed or unknown hash returns false instead of throwing.
    /// </summary>
    bool Verify(string password, string passwordHash);
}
