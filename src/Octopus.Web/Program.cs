using Octopus.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (OpenTelemetry, health checks, service discovery, resilience)
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Octopus.Blazor with PlatformConnected mode using configuration
// In Development auth mode, no token is required
// When running via AppHost, service discovery resolves "http://octopus-server" to the actual endpoint
builder.Services.AddOctopusBlazorPlatformConnected(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Map Aspire default endpoints (/health, /alive)
app.MapDefaultEndpoints();

app.MapRazorComponents<Octopus.Web.App>()
    .AddInteractiveServerRenderMode();

app.Run();
