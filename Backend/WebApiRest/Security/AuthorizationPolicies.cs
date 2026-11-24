using Domain.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace WebApiRest.Security;

/// <summary>Role-based policies. Every endpoint needs a valid token unless it is marked [AllowAnonymous].</summary>
public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);

    /// <summary>Patients and appointments: reception and admin.</summary>
    public const string StaffWithAgendaAccess = nameof(StaffWithAgendaAccess);

    /// <summary>Clinical work (RN-09, RN-15): dentist and admin.</summary>
    public const string ClinicalAccess = nameof(ClinicalAccess);

    public static IServiceCollection AddCitaDentAuthorization(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AdminOnly, policy => policy.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(StaffWithAgendaAccess, policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Reception)))
            .AddPolicy(ClinicalAccess, policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Dentist)));

        return services;
    }
}
