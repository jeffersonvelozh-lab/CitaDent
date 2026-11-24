using System.Text;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace WebApiRest.Security;

/// <summary>
/// Validates incoming tokens with the same <see cref="JwtOptions"/> the issuer signs them with,
/// so issuer, audience and key live in one place.
/// </summary>
public sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions) : IConfigureNamedOptions<JwtBearerOptions>
{
    // Small tolerance for clock differences; an expired token stops working almost at once.
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var settings = jwtOptions.Value;

        // Keep the claim names as issued ("sub", "role") instead of the long WS-* names.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = ClockSkew,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = JwtAccessTokenIssuer.RoleClaim
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
}
