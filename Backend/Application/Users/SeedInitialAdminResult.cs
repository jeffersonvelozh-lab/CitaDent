namespace Application.Users;

public enum SeedInitialAdminResult
{
    /// <summary>There was no administrator, so one was created.</summary>
    Created = 1,

    /// <summary>An administrator already exists (active or not); nothing changed.</summary>
    AdminAlreadyExists = 2,

    /// <summary>No administrator exists, but another user already has the configured email; nothing changed.</summary>
    EmailAlreadyInUse = 3,
}
