using Domain.Users;

namespace Application.Dentists;

/// <summary>The full name of a dentist's user account: first name, a space and last name.</summary>
internal static class DentistUserName
{
    // Two names of up to 80 characters can pass the user's 160, so it is cut. Blank names are left
    // for the Dentist entity to reject.
    public static string From(string? firstName, string? lastName)
    {
        var fullName = $"{firstName?.Trim()} {lastName?.Trim()}".Trim();

        return fullName.Length > User.FullNameMaxLength ? fullName[..User.FullNameMaxLength] : fullName;
    }
}
