using Application.Common;
using Application.Security;
using Application.Users;
using Domain.Common;
using Domain.Users;

namespace Application.Tests.Users;

public class SeedInitialAdminHandlerTests
{
    private const string Password = "Clave-Admin-2026";

    private readonly FakeUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private SeedInitialAdminHandler Handler => new(_users, new FakePasswordHasher(), _unitOfWork);

    private static SeedInitialAdminCommand Command(string email = "Admin@CitaDent.test", string password = Password) =>
        new(email, "Administración", password);

    [Fact]
    public async Task Handle_WhenThereIsNoAdmin_CreatesAnActiveAdminWithTheNormalizedEmail()
    {
        var result = await Handler.HandleAsync(Command());

        Assert.Equal(SeedInitialAdminResult.Created, result);
        var admin = Assert.Single(_users.Added);
        Assert.Equal("admin@citadent.test", admin.Email);
        Assert.Equal("Administración", admin.FullName);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.True(admin.IsActive);
    }

    [Fact]
    public async Task Handle_WhenItCreatesTheAdmin_SavesExactlyOnce()
    {
        await Handler.HandleAsync(Command());

        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_StoresTheHashNotThePlainPassword()
    {
        await Handler.HandleAsync(Command());

        var admin = Assert.Single(_users.Added);
        Assert.NotEqual(Password, admin.PasswordHash);
        Assert.Equal($"hashed:{Password}", admin.PasswordHash);
    }

    [Fact]
    public async Task Handle_WhenAnAdminAlreadyExists_DoesNothing()
    {
        _users.Seed(User.Create("otro-admin@citadent.test", "Otro Admin", "hash", UserRole.Admin));

        var result = await Handler.HandleAsync(Command());

        Assert.Equal(SeedInitialAdminResult.AdminAlreadyExists, result);
        Assert.Empty(_users.Added);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_WhenTheOnlyAdminIsInactive_StillDoesNothing()
    {
        var inactiveAdmin = User.Create("otro-admin@citadent.test", "Otro Admin", "hash", UserRole.Admin);
        inactiveAdmin.Deactivate();
        _users.Seed(inactiveAdmin);

        var result = await Handler.HandleAsync(Command());

        Assert.Equal(SeedInitialAdminResult.AdminAlreadyExists, result);
        Assert.Empty(_users.Added);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_WhenAnotherUserHasTheEmail_DoesNotTouchIt()
    {
        _users.Seed(User.Create("admin@citadent.test", "Recepción", "hash", UserRole.Reception));

        var result = await Handler.HandleAsync(Command());

        Assert.Equal(SeedInitialAdminResult.EmailAlreadyInUse, result);
        Assert.Empty(_users.Added);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta")]
    [InlineData("once-chars!")] // 11 characters, one short of the minimum.
    public async Task Handle_WithAShortPassword_ThrowsValidation(string password)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler.HandleAsync(Command(password: password)));

        Assert.Equal("VALIDATION", ex.Code);
        Assert.Empty(_users.Added);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_WithAShortPassword_ThrowsEvenWhenAnAdminExists()
    {
        _users.Seed(User.Create("otro-admin@citadent.test", "Otro Admin", "hash", UserRole.Admin));

        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler.HandleAsync(Command(password: "corta")));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public async Task Handle_WithAnInvalidEmail_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler.HandleAsync(Command(email: "no-es-email")));

        Assert.Equal("VALIDATION", ex.Code);
        Assert.Empty(_users.Added);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _existing = [];

        public List<User> Added { get; } = [];

        public void Seed(User user) => _existing.Add(user);

        private IEnumerable<User> All => _existing.Concat(Added);

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(All.SingleOrDefault(u => u.Email == email));

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(All.SingleOrDefault(u => u.Id == id));

        public Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken cancellationToken = default) =>
            Task.FromResult(All.Any(u => u.Role == role));

        public void Add(User user) => Added.Add(user);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == $"hashed:{password}";
    }
}
