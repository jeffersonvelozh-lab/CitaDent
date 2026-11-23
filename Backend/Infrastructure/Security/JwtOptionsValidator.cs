using System.Text;
using Microsoft.Extensions.Options;

namespace Infrastructure.Security;

/// <summary>Rejects missing or weak JWT settings when the application starts.</summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    // HMAC-SHA256 needs a key of at least 256 bits.
    public const int MinimumSigningKeyBytes = 32;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            errors.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add("Jwt:Audience is required.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) < MinimumSigningKeyBytes)
        {
            errors.Add($"Jwt:SigningKey is required and must have at least {MinimumSigningKeyBytes} bytes.");
        }

        if (options.ExpirationMinutes <= 0)
        {
            errors.Add("Jwt:ExpirationMinutes must be a positive whole number.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
