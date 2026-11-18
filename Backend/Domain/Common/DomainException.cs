namespace Domain.Common;

/// <summary>
/// Raised when a business rule is broken. <see cref="Code"/> holds the rule id from the project plan
/// (for example "RN-02") so the API and the Spanish UI can map it to a friendly message.
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}