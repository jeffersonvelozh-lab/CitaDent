namespace Domain.Common;

/// <summary>
/// Raised when a business rule is broken. <see cref="Code"/> holds the rule id from the project plan
/// (for example "RN-02") so the API and the Spanish UI can map it to a friendly message.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Code for missing or malformed data (HTTP 400), as opposed to a business rule (RN-xx).</summary>
    public const string ValidationCode = "VALIDATION";

    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}