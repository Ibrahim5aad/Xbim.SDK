using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Octopus.Server.Abstractions.Auth;

namespace Octopus.Server.App.Auth;

/// <summary>
/// Options for configuring the Octopus authorization service.
/// </summary>
public class OctopusAuthorizationOptions
{
    /// <summary>
    /// Gets or sets whether workspace Members have implicit Viewer access to all projects in the workspace.
    /// Default is true.
    /// </summary>
    public bool WorkspaceMemberImplicitProjectAccess { get; set; } = true;
}

/// <summary>
/// Extension methods for configuring authentication in Octopus.Server.
/// </summary>
public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Adds development authentication mode with a fixed principal.
    /// This is intended for local development without requiring an external identity provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure the dev auth options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOctopusDevAuth(
        this IServiceCollection services,
        Action<DevAuthenticationOptions>? configureOptions = null)
    {
        // Add HttpContextAccessor for IUserContext
        services.AddHttpContextAccessor();

        // Register IUserContext
        services.AddScoped<IUserContext, HttpContextUserContext>();

        // Register IAuthorizationService
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        // Configure authentication
        services.AddAuthentication(DevAuthenticationHandler.SchemeName)
            .AddScheme<DevAuthenticationOptions, DevAuthenticationHandler>(
                DevAuthenticationHandler.SchemeName,
                configureOptions ?? (_ => { }));

        // Add authorization
        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Adds OIDC/JWT bearer authentication mode.
    /// Validates tokens via Authority and Audience configuration.
    /// Maps sub/email/name claims and auto-provisions local User via UserProvisioningMiddleware.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing Auth:OIDC settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOctopusOidcAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add HttpContextAccessor for IUserContext
        services.AddHttpContextAccessor();

        // Register IUserContext
        services.AddScoped<IUserContext, HttpContextUserContext>();

        // Register IAuthorizationService
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        // Get OIDC configuration
        var oidcSection = configuration.GetSection("Auth:OIDC");
        var authority = oidcSection.GetValue<string>("Authority");
        var audience = oidcSection.GetValue<string>("Audience");
        var requireHttpsMetadata = oidcSection.GetValue<bool?>("RequireHttpsMetadata") ?? true;

        if (string.IsNullOrEmpty(authority))
        {
            throw new InvalidOperationException("Auth:OIDC:Authority must be configured for OIDC authentication mode.");
        }

        // Configure JWT Bearer authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.RequireHttpsMetadata = requireHttpsMetadata;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = !string.IsNullOrEmpty(audience),
                ValidAudience = audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                // Map standard OIDC claims
                NameClaimType = "name",
                RoleClaimType = "roles"
            };

            // Map additional claims from the token
            options.MapInboundClaims = false; // Preserve original claim names (sub, email, name)
        });

        // Add authorization
        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Adds the Octopus user context service without configuring authentication.
    /// Use this when authentication is configured separately (e.g., OIDC/JWT).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOctopusUserContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpContextUserContext>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        return services;
    }
}
