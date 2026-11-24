using Application.Security;

namespace Application.Tests.Fakes;

/// <summary>A readable stand-in for the real hasher, so a test can see what was hashed.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public static string HashOf(string password) => $"hashed:{password}";

    public string Hash(string password) => HashOf(password);

    public bool Verify(string password, string passwordHash) => passwordHash == HashOf(password);
}
