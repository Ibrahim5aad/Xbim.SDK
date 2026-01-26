using Microsoft.EntityFrameworkCore;
using Octopus.Server.Abstractions.Auth;
using Octopus.Server.App.Auth;
using Octopus.Server.Persistence.EfCore;
using Octopus.Server.Persistence.EfCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (OpenTelemetry, health checks, resilience)
builder.AddServiceDefaults();

// Add persistence (SQLite for development by default)
var connectionString = builder.Configuration.GetConnectionString("OctopusDb") ?? "Data Source=octopus.db";
builder.Services.AddOctopusSqlite(connectionString);

// Configure authentication mode based on configuration
var authMode = builder.Configuration.GetValue<string>("Auth:Mode") ?? "Development";

if (authMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
{
    // Development auth mode: inject a fixed principal
    builder.Services.AddOctopusDevAuth(options =>
    {
        options.Subject = builder.Configuration.GetValue<string>("Auth:Dev:Subject") ?? "dev-user";
        options.Email = builder.Configuration.GetValue<string>("Auth:Dev:Email") ?? "dev@localhost";
        options.DisplayName = builder.Configuration.GetValue<string>("Auth:Dev:DisplayName") ?? "Development User";
    });
}
else
{
    // For other modes (OIDC), just add the user context service
    // OIDC configuration will be added in M2-003
    builder.Services.AddOctopusUserContext();
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Octopus Server API",
        Version = "v1",
        Description = "BIM backend API for the Octopus platform"
    });
});

var app = builder.Build();

// Apply pending migrations on startup (development convenience)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OctopusDbContext>();
    dbContext.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Octopus Server API v1");
    options.RoutePrefix = "swagger";
});

// Authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// User provisioning middleware (auto-creates User entity from authenticated principal)
app.UseUserProvisioning();

// Map Aspire default endpoints (/health, /alive)
app.MapDefaultEndpoints();

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }))
   .WithName("HealthCheck")
   .WithOpenApi();

// Debug endpoint to verify user context (development only)
app.MapGet("/api/v1/me", (IUserContext userContext) =>
{
    if (!userContext.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        userId = userContext.UserId,
        subject = userContext.Subject,
        email = userContext.Email,
        displayName = userContext.DisplayName,
        isAuthenticated = userContext.IsAuthenticated
    });
})
.WithName("GetCurrentUser")
.WithOpenApi()
.RequireAuthorization();

app.Run();
