using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Security;

/// <summary>
/// Settings for issuing access tokens, read from the "Jwt" configuration section. The signing key is a
/// secret: in development it comes from user-secrets, elsewhere from an environment variable (Jwt__SigningKey).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int DefaultExpirationMinutes = 60;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = DefaultExpirationMinutes;

    /// <summary>
    /// Copies the "Jwt" section into the options. A present but non-numeric ExpirationMinutes becomes 0,
    /// so the validator rejects it instead of silently using the default.
    /// </summary>
    public static void ReadFrom(IConfiguration configuration, JwtOptions options)
    {
        var section = configuration.GetSection(SectionName);

        options.Issuer = section[nameof(Issuer)] ?? string.Empty;
        options.Audience = section[nameof(Audience)] ?? string.Empty;
        options.SigningKey = section[nameof(SigningKey)] ?? string.Empty;

        if (section[nameof(ExpirationMinutes)] is { } minutes)
        {
            options.ExpirationMinutes =
                int.TryParse(minutes, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }
    }
}
