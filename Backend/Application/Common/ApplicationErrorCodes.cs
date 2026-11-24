namespace Application.Common;

/// <summary>Error codes raised by use cases that are not tied to one business rule.</summary>
public static class ApplicationErrorCodes
{
    /// <summary>The requested record does not exist (HTTP 404).</summary>
    public const string NotFound = "NOT-FOUND";

    /// <summary>Another record already has a value that must be unique (HTTP 409).</summary>
    public const string Duplicate = "DUPLICATE";
}
