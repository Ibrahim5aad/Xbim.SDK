using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Octopus.Server.Abstractions.Processing;
using Octopus.Server.App.Auth;
using Octopus.Server.Contracts;
using Octopus.Server.Domain.Entities;
using Octopus.Server.Persistence.EfCore;

using WorkspaceRole = Octopus.Server.Domain.Enums.WorkspaceRole;
using DomainClientType = Octopus.Server.Domain.Enums.OAuthClientType;

namespace Octopus.Server.App.Tests.Endpoints;

public class OAuthEndpointsTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testDbName;
    private readonly TestInMemoryProcessingQueue _processingQueue;

    public OAuthEndpointsTests()
    {
        _testDbName = $"test_{Guid.NewGuid()}";
        _processingQueue = new TestInMemoryProcessingQueue();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<OctopusDbContext>));
                services.RemoveAll(typeof(DbContextOptions));
                services.RemoveAll(typeof(OctopusDbContext));
                services.RemoveAll(typeof(IProcessingQueue));
                services.AddSingleton<IProcessingQueue>(_processingQueue);

                services.AddDbContext<OctopusDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_testDbName);
                });
            });
        });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // Important: don't follow redirects so we can inspect them
        });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<(Guid WorkspaceId, Guid DevUserId)> CreateTestWorkspaceWithAdminUser()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OctopusDbContext>();

        var devUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == devUserId);
        if (user == null)
        {
            user = new User
            {
                Id = devUserId,
                Subject = "dev-user",
                Email = "dev@example.com",
                DisplayName = "Dev User",
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Users.Add(user);
        }

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Test Workspace",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Workspaces.Add(workspace);

        var membership = new WorkspaceMembership
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = devUserId,
            Role = WorkspaceRole.Admin,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.WorkspaceMemberships.Add(membership);

        await dbContext.SaveChangesAsync();

        return (workspace.Id, devUserId);
    }

    private async Task<OAuthApp> CreateTestOAuthApp(Guid workspaceId, Guid userId, bool isPublic = true)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OctopusDbContext>();

        var clientId = $"oct_test_{Guid.NewGuid():N}";
        string? secretHash = null;

        if (!isPublic)
        {
            // Generate and hash a client secret for confidential clients
            var secret = "test_secret_12345";
            using var deriveBytes = new Rfc2898DeriveBytes(
                secret,
                saltSize: 16,
                iterations: 100000,
                HashAlgorithmName.SHA256);
            var salt = deriveBytes.Salt;
            var hash = deriveBytes.GetBytes(32);
            var combined = new byte[48];
            Buffer.BlockCopy(salt, 0, combined, 0, 16);
            Buffer.BlockCopy(hash, 0, combined, 16, 32);
            secretHash = Convert.ToBase64String(combined);
        }

        var app = new OAuthApp
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = "Test OAuth App",
            ClientType = isPublic ? DomainClientType.Public : DomainClientType.Confidential,
            ClientId = clientId,
            ClientSecretHash = secretHash,
            RedirectUris = "[\"https://example.com/callback\", \"http://localhost:3000/callback\"]",
            AllowedScopes = "read write openid",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = userId
        };

        dbContext.OAuthApps.Add(app);
        await dbContext.SaveChangesAsync();

        return app;
    }

    private static (string CodeVerifier, string CodeChallenge) GeneratePkceValues()
    {
        // Generate a random code verifier
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var codeVerifier = Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        // Generate S256 code challenge
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = Convert.ToBase64String(challengeBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        return (codeVerifier, codeChallenge);
    }

    [Fact]
    public async Task Authorize_ReturnsRedirect_WithAuthorizationCode()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: true);
        var (codeVerifier, codeChallenge) = GeneratePkceValues();

        var redirectUri = "https://example.com/callback";
        var state = "test_state_123";

        // Act
        var response = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location!;
        Assert.StartsWith(redirectUri, location.ToString());

        var query = HttpUtility.ParseQueryString(location.Query);
        Assert.NotNull(query["code"]);
        Assert.Equal(state, query["state"]);
    }

    [Fact]
    public async Task Authorize_ReturnsBadRequest_WhenClientIdIsUnknown()
    {
        // Act
        var response = await _client.GetAsync(
            "/oauth/authorize?response_type=code&client_id=unknown_client&redirect_uri=https://example.com/callback");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        Assert.Equal(OAuthErrorCodes.InvalidRequest, error!.Error);
    }

    [Fact]
    public async Task Authorize_ReturnsBadRequest_WhenRedirectUriNotRegistered()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: true);

        // Act - use unregistered redirect URI
        var response = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri=https://attacker.com/callback&scope=read");

        // Assert - MUST NOT redirect, return error directly
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        Assert.Equal(OAuthErrorCodes.InvalidRequest, error!.Error);
        Assert.Contains("redirect_uri", error.ErrorDescription);
    }

    [Fact]
    public async Task Authorize_ReturnsError_WhenPkceNotProvidedForPublicClient()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: true);
        var redirectUri = "https://example.com/callback";

        // Act - no PKCE parameters for public client
        var response = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read");

        // Assert - redirects with error
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location!;
        var query = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal(OAuthErrorCodes.InvalidRequest, query["error"]);
        Assert.Contains("code_challenge", query["error_description"]);
    }

    [Fact]
    public async Task Authorize_ReturnsError_WhenScopeInvalid()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: true);
        var (_, codeChallenge) = GeneratePkceValues();
        var redirectUri = "https://example.com/callback";

        // Act - request scope not in allowed scopes
        var response = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=invalid_scope&code_challenge={codeChallenge}&code_challenge_method=S256");

        // Assert - redirects with error
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location!;
        var query = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal(OAuthErrorCodes.InvalidScope, query["error"]);
    }

    [Fact]
    public async Task Token_ExchangesCodeForAccessToken()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: true);
        var (codeVerifier, codeChallenge) = GeneratePkceValues();
        var redirectUri = "https://example.com/callback";

        // First, get an authorization code
        var authResponse = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read+write&code_challenge={codeChallenge}&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.Redirect, authResponse.StatusCode);
        var location = authResponse.Headers.Location!;
        var code = HttpUtility.ParseQueryString(location.Query)["code"];
        Assert.NotNull(code);

        // Act - exchange code for token
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = app.ClientId,
            ["code_verifier"] = codeVerifier
        });

        var tokenResponse = await _client.PostAsync("/oauth/token", tokenRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var token = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenResponse>();
        Assert.NotNull(token);
        Assert.NotEmpty(token!.AccessToken);
        Assert.Equal("Bearer", token.TokenType);
        Assert.True(token.ExpiresIn > 0);
        Assert.Contains("read", token.Scope!);
        Assert.Contains("write", token.Scope);
    }

    [Fact]
    public async Task Token_ReturnsError_WhenCodeAlreadyUsed()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: true);
        var (codeVerifier, codeChallenge) = GeneratePkceValues();
        var redirectUri = "https://example.com/callback";

        // Get an authorization code
        var authResponse = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read&code_challenge={codeChallenge}&code_challenge_method=S256");

        var code = HttpUtility.ParseQueryString(authResponse.Headers.Location!.Query)["code"];

        // Exchange code once (success)
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = app.ClientId,
            ["code_verifier"] = codeVerifier
        });
        var firstResponse = await _client.PostAsync("/oauth/token", tokenRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act - try to use code again
        var secondRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = app.ClientId,
            ["code_verifier"] = codeVerifier
        });
        var secondResponse = await _client.PostAsync("/oauth/token", secondRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);

        var error = await secondResponse.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        Assert.Equal(OAuthErrorCodes.InvalidGrant, error!.Error);
        Assert.Contains("already been used", error.ErrorDescription);
    }

    [Fact]
    public async Task Token_ReturnsError_WhenCodeVerifierInvalid()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: true);
        var (_, codeChallenge) = GeneratePkceValues();
        var redirectUri = "https://example.com/callback";

        // Get an authorization code
        var authResponse = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read&code_challenge={codeChallenge}&code_challenge_method=S256");

        var code = HttpUtility.ParseQueryString(authResponse.Headers.Location!.Query)["code"];

        // Act - use wrong code_verifier
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = app.ClientId,
            ["code_verifier"] = "wrong_verifier"
        });
        var tokenResponse = await _client.PostAsync("/oauth/token", tokenRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, tokenResponse.StatusCode);

        var error = await tokenResponse.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        Assert.Equal(OAuthErrorCodes.InvalidGrant, error!.Error);
        Assert.Contains("code_verifier", error.ErrorDescription);
    }

    [Fact]
    public async Task Token_ReturnsError_WhenRedirectUriDoesNotMatch()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: true);
        var (codeVerifier, codeChallenge) = GeneratePkceValues();
        var redirectUri = "https://example.com/callback";

        // Get an authorization code
        var authResponse = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read&code_challenge={codeChallenge}&code_challenge_method=S256");

        var code = HttpUtility.ParseQueryString(authResponse.Headers.Location!.Query)["code"];

        // Act - use different redirect_uri
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = "http://localhost:3000/callback", // Different URI
            ["client_id"] = app.ClientId,
            ["code_verifier"] = codeVerifier
        });
        var tokenResponse = await _client.PostAsync("/oauth/token", tokenRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, tokenResponse.StatusCode);

        var error = await tokenResponse.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        Assert.Equal(OAuthErrorCodes.InvalidGrant, error!.Error);
        Assert.Contains("redirect_uri", error.ErrorDescription);
    }

    [Fact]
    public async Task Token_RequiresClientSecret_ForConfidentialClient()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: false);
        var redirectUri = "https://example.com/callback";

        // Confidential clients can use authorization without PKCE (though PKCE is still recommended)
        var authResponse = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read");

        Assert.Equal(HttpStatusCode.Redirect, authResponse.StatusCode);
        var code = HttpUtility.ParseQueryString(authResponse.Headers.Location!.Query)["code"];

        // Act - try to exchange without client_secret
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = app.ClientId
        });
        var tokenResponse = await _client.PostAsync("/oauth/token", tokenRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, tokenResponse.StatusCode);

        var error = await tokenResponse.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        Assert.Equal(OAuthErrorCodes.InvalidClient, error!.Error);
        Assert.Contains("client_secret", error.ErrorDescription);
    }

    [Fact]
    public async Task Token_AuthenticatesConfidentialClient_WithValidSecret()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: false);
        var redirectUri = "https://example.com/callback";

        var authResponse = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read");

        var code = HttpUtility.ParseQueryString(authResponse.Headers.Location!.Query)["code"];

        // Act - exchange with correct client_secret
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = app.ClientId,
            ["client_secret"] = "test_secret_12345"
        });
        var tokenResponse = await _client.PostAsync("/oauth/token", tokenRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var token = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenResponse>();
        Assert.NotNull(token);
        Assert.NotEmpty(token!.AccessToken);
    }

    [Fact]
    public async Task Token_ReturnsError_WhenGrantTypeInvalid()
    {
        // Act
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "some_client"
        });
        var tokenResponse = await _client.PostAsync("/oauth/token", tokenRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, tokenResponse.StatusCode);

        var error = await tokenResponse.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        Assert.Equal(OAuthErrorCodes.UnsupportedGrantType, error!.Error);
    }

    [Fact]
    public async Task AccessToken_ContainsExpectedClaims()
    {
        // Arrange
        var (workspaceId, userId) = await CreateTestWorkspaceWithAdminUser();
        var app = await CreateTestOAuthApp(workspaceId, userId, isPublic: true);
        var (codeVerifier, codeChallenge) = GeneratePkceValues();
        var redirectUri = "https://example.com/callback";

        // Get authorization code
        var authResponse = await _client.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={app.ClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read+write&code_challenge={codeChallenge}&code_challenge_method=S256");

        var code = HttpUtility.ParseQueryString(authResponse.Headers.Location!.Query)["code"];

        // Exchange for token
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = app.ClientId,
            ["code_verifier"] = codeVerifier
        });
        var tokenResponse = await _client.PostAsync("/oauth/token", tokenRequest);
        var token = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenResponse>();

        // Act - decode JWT to check claims
        var parts = token!.AccessToken.Split('.');
        Assert.Equal(3, parts.Length); // JWT has 3 parts

        var payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
            Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(parts[1]))));

        // Assert - check expected claims
        Assert.NotNull(payload);
        Assert.Equal("dev-user", payload!["sub"].ToString());
        Assert.Equal(workspaceId.ToString(), payload["tid"].ToString()); // Workspace ID
        Assert.Contains("read", payload["scp"].ToString()!); // Scopes
        Assert.Contains("write", payload["scp"].ToString()!);
        Assert.Equal(app.ClientId, payload["client_id"].ToString());
    }

    private static string PadBase64(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: return base64 + "==";
            case 3: return base64 + "=";
            default: return base64;
        }
    }
}
