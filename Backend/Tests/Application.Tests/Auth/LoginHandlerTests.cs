using Application.Auth;
using Application.Security;
using Application.Users;
using Domain.Common;
using Domain.Users;
using Xunit;

namespace Application.Tests.Auth;

public class LoginHandlerTests
{
    private const string Password = "Correct-Horse-42";

    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeTokenIssuer _tokens = new();

    private LoginHandler Handler => new(_users, _hasher, _tokens);

    private User AddUser(string email = "ana@citadent.ec", UserRole role = UserRole.Reception)
    {
        var user = User.Create(email, "Ana Torres", _hasher.Hash(Password), role);
        _users.Add(user);
        _hasher.HashCalls = 0;
        return user;
    }

    [Fact]
    public async Task Login_ReturnsTheTokenAndRole_ForValidCredentials()
    {
        var user = AddUser(role: UserRole.Dentist);

        var result = await Handler.HandleAsync(new LoginCommand("ana@citadent.ec", Password));

        Assert.Equal($"token-for-{user.Id}", result.AccessToken);
        Assert.Equal(FakeTokenIssuer.ExpiresAt, result.ExpiresAt);
        Assert.Equal(UserRole.Dentist, result.Role);
    }

    [Fact]
    public async Task Login_FindsTheUser_WhenTheEmailHasCapitalsOrSpaces()
    {
        AddUser();

        var result = await Handler.HandleAsync(new LoginCommand("  Ana@CitaDent.EC ", Password));

        Assert.Equal(UserRole.Reception, result.Role);
    }

    [Fact]
    public async Task Login_Fails_ForAWrongPassword()
    {
        AddUser();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(new LoginCommand("ana@citadent.ec", "wrong-password")));

        Assert.Equal(LoginHandler.InvalidCredentialsCode, ex.Code);
        Assert.Equal(0, _tokens.IssuedCount);
    }

    [Fact]
    public async Task Login_Fails_WithTheSameError_ForAnUnknownEmail()
    {
        AddUser();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(new LoginCommand("nobody@citadent.ec", Password)));

        Assert.Equal(LoginHandler.InvalidCredentialsCode, ex.Code);
        Assert.Equal(0, _tokens.IssuedCount);
    }

    [Fact]
    public async Task Login_StillHashes_ForAnUnknownEmail_SoTheTimeDoesNotGiveItAway()
    {
        await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(new LoginCommand("nobody@citadent.ec", Password)));

        Assert.Equal(1, _hasher.HashCalls);
    }

    [Fact]
    public async Task Login_Fails_WithTheSameError_ForAnInactiveUser()
    {
        var user = AddUser();
        user.Deactivate();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(new LoginCommand("ana@citadent.ec", Password)));

        Assert.Equal(LoginHandler.InvalidCredentialsCode, ex.Code);
        Assert.Equal(0, _tokens.IssuedCount);
    }

    [Theory]
    [InlineData(null, Password)]
    [InlineData("", Password)]
    [InlineData("   ", Password)]
    [InlineData("ana@citadent.ec", null)]
    [InlineData("ana@citadent.ec", "")]
    public async Task Login_FailsValidation_WhenEmailOrPasswordIsMissing(string? email, string? password)
    {
        AddUser();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(new LoginCommand(email!, password!)));

        Assert.Equal("VALIDATION", ex.Code);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _users = [];

        public void Add(User user) => _users.Add(user);

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.SingleOrDefault(u => u.Email == email));

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.SingleOrDefault(u => u.Id == id));

        public Task<bool> EmailExistsAsync(string email, Guid? excludingId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<User>> ListAsync(
            bool includeInactive,
            UserRole? role,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> AnyOtherActiveAdminAsync(Guid excludingId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.Any(u => u.Role == role));
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public int HashCalls { get; set; }

        public string Hash(string password)
        {
            HashCalls++;
            return $"hashed:{password}";
        }

        public bool Verify(string password, string passwordHash) => passwordHash == $"hashed:{password}";
    }

    private sealed class FakeTokenIssuer : IAccessTokenIssuer
    {
        public static readonly DateTimeOffset ExpiresAt = new(2026, 12, 1, 15, 0, 0, TimeSpan.Zero);

        public int IssuedCount { get; private set; }

        public AccessToken Issue(User user)
        {
            IssuedCount++;
            return new AccessToken($"token-for-{user.Id}", ExpiresAt);
        }
    }
}
