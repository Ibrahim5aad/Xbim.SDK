using System.Text.Json;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Octopus.Server.Abstractions.Auth;
using Octopus.Server.App.Auth;
using Octopus.Server.Contracts;
using Octopus.Server.Domain.Entities;
using Octopus.Server.Persistence.EfCore;

using DomainOAuthClientType = Octopus.Server.Domain.Enums.OAuthClientType;

namespace Octopus.Server.App.Endpoints;

public static class OAuthEndpoints
{
    public static IEndpointRouteBuilder MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Authorization endpoint (browser-based)
        app.MapGet("/oauth/authorize", Authorize)
            .WithName("OAuthAuthorize")
            .WithTags("OAuth")
            .Produces(StatusCodes.Status302Found)
            .Produces<OAuthErrorResponse>(StatusCodes.Status400BadRequest)
            .WithOpenApi(operation =>
            {
                operation.Summary = "OAuth 2.0 Authorization Endpoint";
                operation.Description = "Initiates the OAuth 2.0 authorization code flow with PKCE support.";
                return operation;
            })
            .RequireAuthorization();

        // Token endpoint (API)
        app.MapPost("/oauth/token", Token)
            .WithName("OAuthToken")
            .WithTags("OAuth")
            .Accepts<AuthorizationCodeTokenRequest>("application/x-www-form-urlencoded")
            .Produces<OAuthTokenResponse>()
            .Produces<OAuthErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<OAuthErrorResponse>(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "OAuth 2.0 Token Endpoint";
                operation.Description = "Exchanges an authorization code for access tokens.";
                return operation;
            })
            .AllowAnonymous();

        return app;
    }

    /// <summary>
    /// OAuth 2.0 Authorization Endpoint (RFC 6749 Section 4.1.1)
    /// </summary>
    private static async Task<IResult> Authorize(
        HttpContext httpContext,
        IUserContext userContext,
        IOAuthTokenService tokenService,
        OctopusDbContext dbContext,
        string response_type,
        string client_id,
        string redirect_uri,
        string? scope = null,
        string? state = null,
        string? code_challenge = null,
        string? code_challenge_method = null,
        CancellationToken cancellationToken = default)
    {
        // Validate user is authenticated
        if (!userContext.IsAuthenticated || !userContext.UserId.HasValue || string.IsNullOrEmpty(userContext.Subject))
        {
            return Results.Unauthorized();
        }

        // Validate response_type (must be "code" for authorization code flow)
        if (response_type != "code")
        {
            // Invalid response_type - return error via redirect if possible
            if (!string.IsNullOrEmpty(redirect_uri) && Uri.TryCreate(redirect_uri, UriKind.Absolute, out _))
            {
                return RedirectWithError(redirect_uri, OAuthErrorCodes.UnsupportedResponseType,
                    "Only 'code' response type is supported.", state);
            }
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.UnsupportedResponseType,
                ErrorDescription = "Only 'code' response type is supported."
            });
        }

        // Validate client_id
        if (string.IsNullOrEmpty(client_id))
        {
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidRequest,
                ErrorDescription = "client_id is required."
            });
        }

        // Look up the OAuth app
        var app = await dbContext.OAuthApps
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ClientId == client_id, cancellationToken);

        if (app == null)
        {
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidRequest,
                ErrorDescription = "Unknown client_id."
            });
        }

        if (!app.IsEnabled)
        {
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.UnauthorizedClient,
                ErrorDescription = "The client application is disabled."
            });
        }

        // Validate redirect_uri - CRITICAL: must validate BEFORE redirecting
        var registeredUris = JsonSerializer.Deserialize<List<string>>(app.RedirectUris) ?? new List<string>();
        if (string.IsNullOrEmpty(redirect_uri) || !registeredUris.Contains(redirect_uri))
        {
            // DO NOT redirect - invalid redirect_uri is a security issue
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidRequest,
                ErrorDescription = "redirect_uri is not registered for this client."
            });
        }

        // Validate PKCE for public clients (required) and confidential clients (recommended)
        if (app.ClientType == DomainOAuthClientType.Public)
        {
            if (string.IsNullOrEmpty(code_challenge))
            {
                return RedirectWithError(redirect_uri, OAuthErrorCodes.InvalidRequest,
                    "code_challenge is required for public clients.", state);
            }
            if (string.IsNullOrEmpty(code_challenge_method) || code_challenge_method != "S256")
            {
                return RedirectWithError(redirect_uri, OAuthErrorCodes.InvalidRequest,
                    "code_challenge_method must be 'S256' for public clients.", state);
            }
        }

        // Validate code_challenge_method if provided
        if (!string.IsNullOrEmpty(code_challenge_method) && code_challenge_method != "S256" && code_challenge_method != "plain")
        {
            return RedirectWithError(redirect_uri, OAuthErrorCodes.InvalidRequest,
                "code_challenge_method must be 'S256' or 'plain'.", state);
        }

        // Validate and filter scopes
        var requestedScopes = (scope ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var allowedScopes = app.AllowedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        // Filter to only allowed scopes
        var grantedScopes = requestedScopes.Where(s => allowedScopes.Contains(s)).ToList();

        // If no valid scopes requested, grant all allowed scopes (default behavior)
        if (!grantedScopes.Any() && allowedScopes.Any())
        {
            grantedScopes = allowedScopes.ToList();
        }

        // Check if any requested scope is not allowed
        var invalidScopes = requestedScopes.Except(allowedScopes).ToList();
        if (invalidScopes.Any())
        {
            return RedirectWithError(redirect_uri, OAuthErrorCodes.InvalidScope,
                $"Invalid scope(s): {string.Join(", ", invalidScopes)}", state);
        }

        // Generate authorization code
        var code = tokenService.GenerateAuthorizationCode();
        var codeHash = tokenService.HashCode(code);

        // Store authorization code
        var authCode = new AuthorizationCode
        {
            Id = Guid.NewGuid(),
            CodeHash = codeHash,
            OAuthAppId = app.Id,
            UserId = userContext.UserId.Value,
            WorkspaceId = app.WorkspaceId,
            Scopes = string.Join(" ", grantedScopes),
            RedirectUri = redirect_uri,
            CodeChallenge = code_challenge,
            CodeChallengeMethod = code_challenge_method ?? (string.IsNullOrEmpty(code_challenge) ? null : "plain"),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = tokenService.GetAuthorizationCodeExpiration(),
            IsUsed = false
        };

        dbContext.AuthorizationCodes.Add(authCode);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Redirect with authorization code
        var redirectUrl = BuildRedirectUrl(redirect_uri, code, state);
        return Results.Redirect(redirectUrl);
    }

    /// <summary>
    /// OAuth 2.0 Token Endpoint (RFC 6749 Section 4.1.3)
    /// </summary>
    private static async Task<IResult> Token(
        HttpRequest request,
        IOAuthTokenService tokenService,
        OctopusDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        // Parse form data
        var form = await request.ReadFormAsync(cancellationToken);

        var grantType = form["grant_type"].FirstOrDefault();
        var code = form["code"].FirstOrDefault();
        var redirectUri = form["redirect_uri"].FirstOrDefault();
        var clientId = form["client_id"].FirstOrDefault();
        var clientSecret = form["client_secret"].FirstOrDefault();
        var codeVerifier = form["code_verifier"].FirstOrDefault();

        // Validate grant_type
        if (grantType != "authorization_code")
        {
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.UnsupportedGrantType,
                ErrorDescription = "Only 'authorization_code' grant type is supported."
            });
        }

        // Validate required parameters
        if (string.IsNullOrEmpty(code))
        {
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidRequest,
                ErrorDescription = "code is required."
            });
        }

        if (string.IsNullOrEmpty(clientId))
        {
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidRequest,
                ErrorDescription = "client_id is required."
            });
        }

        if (string.IsNullOrEmpty(redirectUri))
        {
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidRequest,
                ErrorDescription = "redirect_uri is required."
            });
        }

        // Look up the OAuth app
        var app = await dbContext.OAuthApps
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ClientId == clientId, cancellationToken);

        if (app == null)
        {
            return Results.Json(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidClient,
                ErrorDescription = "Unknown client_id."
            }, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!app.IsEnabled)
        {
            return Results.Json(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidClient,
                ErrorDescription = "The client application is disabled."
            }, statusCode: StatusCodes.Status401Unauthorized);
        }

        // Authenticate confidential clients
        if (app.ClientType == DomainOAuthClientType.Confidential)
        {
            if (string.IsNullOrEmpty(clientSecret))
            {
                return Results.Json(new OAuthErrorResponse
                {
                    Error = OAuthErrorCodes.InvalidClient,
                    ErrorDescription = "client_secret is required for confidential clients."
                }, statusCode: StatusCodes.Status401Unauthorized);
            }

            if (!tokenService.ValidateClientSecret(clientSecret, app.ClientSecretHash!))
            {
                return Results.Json(new OAuthErrorResponse
                {
                    Error = OAuthErrorCodes.InvalidClient,
                    ErrorDescription = "Invalid client credentials."
                }, statusCode: StatusCodes.Status401Unauthorized);
            }
        }

        // Look up the authorization code
        var codeHash = tokenService.HashCode(code);
        var authCode = await dbContext.AuthorizationCodes
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CodeHash == codeHash && c.OAuthAppId == app.Id, cancellationToken);

        if (authCode == null)
        {
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidGrant,
                ErrorDescription = "Invalid authorization code."
            });
        }

        // Validate code hasn't been used
        if (authCode.IsUsed)
        {
            // Potential replay attack - revoke all tokens for this code (future feature)
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidGrant,
                ErrorDescription = "Authorization code has already been used."
            });
        }

        // Validate code hasn't expired
        if (authCode.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidGrant,
                ErrorDescription = "Authorization code has expired."
            });
        }

        // Validate redirect_uri matches
        if (authCode.RedirectUri != redirectUri)
        {
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidGrant,
                ErrorDescription = "redirect_uri does not match the authorization request."
            });
        }

        // Validate PKCE if code challenge was provided
        if (!string.IsNullOrEmpty(authCode.CodeChallenge))
        {
            if (string.IsNullOrEmpty(codeVerifier))
            {
                return Results.BadRequest(new OAuthErrorResponse
                {
                    Error = OAuthErrorCodes.InvalidGrant,
                    ErrorDescription = "code_verifier is required."
                });
            }

            if (!tokenService.VerifyPkceChallenge(codeVerifier, authCode.CodeChallenge, authCode.CodeChallengeMethod ?? "plain"))
            {
                return Results.BadRequest(new OAuthErrorResponse
                {
                    Error = OAuthErrorCodes.InvalidGrant,
                    ErrorDescription = "Invalid code_verifier."
                });
            }
        }
        else if (app.ClientType == DomainOAuthClientType.Public)
        {
            // Public clients must always use PKCE
            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = OAuthErrorCodes.InvalidGrant,
                ErrorDescription = "PKCE is required for public clients."
            });
        }

        // Mark code as used
        authCode.IsUsed = true;
        authCode.UsedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Get user subject
        var userSubject = authCode.User?.Subject ?? authCode.UserId.ToString();

        // Generate access token
        var scopes = authCode.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var accessToken = tokenService.GenerateAccessToken(
            subject: userSubject,
            userId: authCode.UserId,
            workspaceId: authCode.WorkspaceId,
            clientId: app.ClientId,
            scopes: scopes);

        var response = new OAuthTokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = tokenService.AccessTokenLifetimeSeconds,
            Scope = authCode.Scopes
        };

        return Results.Ok(response);
    }

    private static string BuildRedirectUrl(string redirectUri, string code, string? state)
    {
        var uriBuilder = new UriBuilder(redirectUri);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["code"] = code;
        if (!string.IsNullOrEmpty(state))
        {
            query["state"] = state;
        }
        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }

    private static IResult RedirectWithError(string redirectUri, string error, string errorDescription, string? state)
    {
        var uriBuilder = new UriBuilder(redirectUri);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["error"] = error;
        query["error_description"] = errorDescription;
        if (!string.IsNullOrEmpty(state))
        {
            query["state"] = state;
        }
        uriBuilder.Query = query.ToString();
        return Results.Redirect(uriBuilder.ToString());
    }
}
