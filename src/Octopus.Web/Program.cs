using Octopus.Blazor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Octopus.Blazor with PlatformConnected mode using configuration
// In Development auth mode, no token is required
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

app.MapRazorComponents<Octopus.Web.App>()
    .AddInteractiveServerRenderMode();

app.Run();
