namespace Domain.Common;

/// <summary>
/// Small helpers for validating input in the domain. Field-level problems use the generic
/// "VALIDATION" code; business rules from the project plan use their own code (RN-xx).
/// </summary>
internal static class Guard
{
    public const string ValidationCode = "VALIDATION";

    public static string NotBlank(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(ValidationCode, $"{field} is required.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(ValidationCode, $"{field} must have at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static string? OptionalMax(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(ValidationCode, $"{field} must have at most {maxLength} characters.");
        }

        return trimmed;
    }
}