using System.Globalization;
using System.Security.Cryptography;
using Application.Security;

namespace Infrastructure.Security;

/// <summary>
/// PBKDF2 with HMAC-SHA256, using only the base class library. The stored value carries everything
/// needed to verify it later: <c>PBKDF2-SHA256$iterations$salt$hash</c> (salt and hash in base64).
/// Because the iteration count travels with each hash, it can be raised later without breaking old hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const string AlgorithmName = "PBKDF2-SHA256";

    // OWASP recommendation for PBKDF2-HMAC-SHA256.
    public const int Iterations = 600_000;

    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const char Separator = '$';

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return string.Join(
            Separator,
            AlgorithmName,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split(Separator);

        if (parts.Length != 4
            || parts[0] != AlgorithmName
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

        // Constant-time comparison, so the time taken does not reveal how many bytes matched.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
