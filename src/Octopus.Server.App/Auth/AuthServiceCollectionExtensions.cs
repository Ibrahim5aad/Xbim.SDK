using Microsoft.AspNetCore.Authentication;
using Octopus.Server.Abstractions.Auth;

namespace Octopus.Server.App.Auth;

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
    /// Adds the Octopus user context service without configuring authentication.
    /// Use this when authentication is configured separately (e.g., OIDC/JWT).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOctopusUserContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpContextUserContext>();
        return services;
    }
}
